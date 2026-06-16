// @tag cli-command
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Job;
using Air.UcpAgent.Cli;

namespace Air.UcpAgent.Cli
{
    public static class CliCommandDiscovery
    {
        static List<IInvokeHandler> _handlers;
        static readonly object Gate = new();

        public static Action<string> LogWarning { get; set; } = static msg => Debug.WriteLine(msg);

        public static IReadOnlyList<IInvokeHandler> Handlers
        {
            get
            {
                EnsureLoaded();
                return _handlers;
            }
        }

        public static void Invalidate() => _handlers = null;

        public static IInvokeHandler Find(string command) => FindForHost(command, hostKind: null);

        public static IInvokeHandler FindForHost(string command, string hostKind)
        {
            EnsureLoaded();
            IInvokeHandler fallback = null;
            foreach (var handler in _handlers)
            {
                if (!string.Equals(handler.Name, command, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.IsNullOrEmpty(hostKind))
                    return handler;

                if (InvokeAvailability.IsAvailableForHost(handler.Scope, hostKind))
                    return handler;

                fallback ??= handler;
            }

            return fallback;
        }

        static void EnsureLoaded()
        {
            if (_handlers != null)
                return;

            lock (Gate)
            {
                if (_handlers != null)
                    return;

                var list = new List<IInvokeHandler>();
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try
                    {
                        types = assembly.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        types = ex.Types.Where(t => t != null).ToArray();
                    }

                    foreach (var type in types)
                    {
                        if (type == null || !type.IsClass || !CliCommandTypeExtensions.IsCliCommand(type))
                            continue;

                        object instance;
                        try
                        {
                            instance = Activator.CreateInstance(type);
                        }
                        catch (Exception ex)
                        {
                            LogWarning(
                                $"[ucp-agent] CLI command type '{type.FullName}' cannot be instantiated: {ex.Message}");
                            continue;
                        }

                        if (instance is not CliCommand)
                        {
                            LogWarning(
                                $"[ucp-agent] CLI command type '{type.FullName}' must inherit CliCommand or CliCommand<T>.");
                            continue;
                        }

                        if (instance is not ICliInvokeDescriptorProvider metadata)
                            continue;

                        var descriptor = metadata.Descriptor;
                        if (descriptor == null)
                            continue;

                        var handler = CreateHandler(type, descriptor);
                        if (handler == null)
                        {
                            LogWarning(
                                $"[ucp-agent] CLI command '{descriptor.Name}' does not implement ICliCommand / ICliCommand<T>.");
                            continue;
                        }

                        list.Add(handler);
                    }
                }

                _handlers = list.OrderBy(h => h.Name, StringComparer.OrdinalIgnoreCase).ToList();
            }
        }

        static IInvokeHandler CreateHandler(Type type, InvokeDescriptor descriptor)
        {
            var paramInterface = type
                .GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICliCommand<>));
            if (paramInterface != null)
            {
                var paramType = paramInterface.GetGenericArguments()[0];
                var handlerType = typeof(SyncHandler<>).MakeGenericType(paramType);
                return Activator.CreateInstance(handlerType, descriptor, type) as IInvokeHandler;
            }

            if (typeof(CliCommand).IsAssignableFrom(type))
                return new NoParamHandler(descriptor, type);

            return null;
        }

        abstract class HandlerBase : IInvokeHandler
        {
            readonly InvokeDescriptor _descriptor;
            readonly Type _paramType;
            string[] _paramDescriptions;

            protected HandlerBase(InvokeDescriptor descriptor, Type fallbackParamType = null)
            {
                _descriptor = descriptor;
                _paramType = descriptor.ParamType ?? fallbackParamType;
            }

            public string Name => _descriptor.Name;
            public CommandHostScope Scope => _descriptor.Scope;
            public string Description => _descriptor.Description ?? "";
            public string Completion =>
                _descriptor is DeferredInvokeDescriptor deferred ? deferred.Completion : "";
            public string[] Aliases => _descriptor.Aliases ?? Array.Empty<string>();
            public int DefaultTimeoutMs => _descriptor.DefaultTimeoutMs;
            public bool AllowConnectionRetry => _descriptor.AllowConnectionRetry;

            public string[] ParamDescriptions =>
                _paramDescriptions ??= InvokeParameterBinding.DescribeOrEmpty(_paramType);

            public void Invoke(IInvocationContext context, IReadOnlyDictionary<string, object> arguments) =>
                InvokeRemote(RequireRemote(context).Context, ToDictionary(arguments));

            public abstract void InvokeRemote(InvokeContext context, Dictionary<string, object> parameters);

            protected static RemoteInvocationContext RequireRemote(IInvocationContext context) =>
                context as RemoteInvocationContext
                ?? throw new ArgumentException("Remote commands require RemoteInvocationContext.", nameof(context));

            protected static Dictionary<string, object> ToDictionary(IReadOnlyDictionary<string, object> arguments) =>
                arguments as Dictionary<string, object>
                ?? arguments?.ToDictionary(p => p.Key, p => p.Value)
                ?? new Dictionary<string, object>();

            protected static Exception Unwrap(Exception ex) =>
                ex is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : ex;
        }

        sealed class NoParamHandler : HandlerBase
        {
            readonly Type _commandType;

            public NoParamHandler(InvokeDescriptor descriptor, Type commandType) : base(descriptor) =>
                _commandType = commandType;

            public override void InvokeRemote(InvokeContext context, Dictionary<string, object> parameters)
            {
                try
                {
                    var command = (CliCommand)CreateCommandInstance(_commandType);
                    BindRuntime(command, context);
                    command.Run();
                }
                catch (Exception ex)
                {
                    throw Unwrap(ex);
                }
            }
        }

        sealed class SyncHandler<TParams> : HandlerBase
        {
            readonly Type _commandType;

            public SyncHandler(InvokeDescriptor descriptor, Type commandType)
                : base(descriptor, typeof(TParams)) => _commandType = commandType;

            public override void InvokeRemote(InvokeContext context, Dictionary<string, object> parameters)
            {
                var (bound, error) = InvokeParameterBinding.BindRequired(typeof(TParams), parameters);
                if (error != null)
                    throw new InvalidOperationException(error);

                try
                {
                    var command = (CliCommand)CreateCommandInstance(_commandType);
                    BindRuntime(command, context);
                    ((ICliCommand<TParams>)command).Run((TParams)bound);
                }
                catch (Exception ex)
                {
                    throw Unwrap(ex);
                }
            }
        }

        sealed class RuntimeAdapter : ICliCommandRuntime
        {
            readonly InvokeContext _context;
            public RuntimeAdapter(InvokeContext context) => _context = context;
            public string CommandId => _context?.CommandId;
            public void CompleteSuccess(object result) => _context?.CompleteSuccess(result);
            public void CompleteFail(string error) => _context?.CompleteFail(error);
            public void MarkRunning() => _context?.MarkRunning();
        }

        static void BindRuntime(CliCommand command, InvokeContext context) =>
            command.BindRuntime(new RuntimeAdapter(context));

        static object CreateCommandInstance(Type type)
        {
            try
            {
                return Activator.CreateInstance(type);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"CLI command type '{type.FullName}' cannot be instantiated: {ex.Message}",
                    ex);
            }
        }
    }
}

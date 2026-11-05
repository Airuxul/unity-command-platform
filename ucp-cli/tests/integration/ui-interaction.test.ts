import { afterAll, beforeAll, describe, expect, it } from "vitest";
import { expectShapesUnchanged, ok, rejected } from "./helpers/content-scenario.js";
import { hasReadyEditorSession } from "./helpers/editor-commands.js";
import { ensureEditMode, runUcpCommand, waitForPlayMode } from "./helpers/play-mode.js";

/**
 * Play Mode UI driving: ui_click, ui_input, ui_wait (and ui_snapshot's root / limit / inputs) on a
 * throwaway canvas built with exec. Everything lives in Play Mode, so stopping removes it.
 */
const runEditorTests = process.env.UCP_EDITOR_INTEGRATION === "1";

const ROOT = "UcpUiProbe";
const USINGS = ["UnityEngine.UI", "UnityEngine.EventSystems", "UnityEditor"];

/** Runs `body` on the editor tick after `seconds`, once; a stopped game makes it a no-op. */
const AFTER = `
void After(double seconds, System.Action body) {
    var due = EditorApplication.timeSinceStartup + seconds;
    EditorApplication.CallbackFunction tick = null;
    tick = () => {
        if (EditorApplication.timeSinceStartup < due) return;
        EditorApplication.update -= tick;
        if (EditorApplication.isPlaying) body();
    };
    EditorApplication.update += tick;
}`;

// DontDestroyOnLoad on purpose: PlayModeNodes has to find nodes outside the loaded scenes too.
// No EventSystem is created, so clicks also have to work without one.
const BUILD_PROBE = `
GameObject Child(string name, Transform parent, params System.Type[] types) {
    var go = new GameObject(name, types);
    go.transform.SetParent(parent, false);
    return go;
}
${AFTER}

var canvas = new GameObject("${ROOT}", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
UnityEngine.Object.DontDestroyOnLoad(canvas);
var root = canvas.transform;

var status = Child("Status", root, typeof(Text)).GetComponent<Text>();
status.text = "idle";

var name = Child("Name", root, typeof(Image), typeof(InputField)).GetComponent<InputField>();
name.textComponent = Child("Text", name.transform, typeof(Text)).GetComponent<Text>();
name.onEndEdit.AddListener(s => status.text = "ended " + s);

var done = Child("Done", root);
Child("DoneLabel", done.transform, typeof(Text)).GetComponent<Text>().text = "all done";
done.SetActive(false);

var go = Child("Go", root, typeof(Image), typeof(Button)).GetComponent<Button>();
Child("Label", go.transform, typeof(Text)).GetComponent<Text>().text = "Go";
go.onClick.AddListener(() => {
    status.text = "clicked " + name.text;
    After(0.3, () => { done.SetActive(true); Child("Spawned", root); });
});

Child("Locked", root, typeof(Image), typeof(Button)).GetComponent<Button>().interactable = false;
Child("Hidden", root, typeof(Image), typeof(Button)).SetActive(false);
return "built";
`;

async function exec(code: string): Promise<void> {
  await ok("exec", { code, usings: USINGS }, 60_000);
}

describe.skipIf(!runEditorTests)("play mode UI interaction (Unity required)", () => {
  beforeAll(async () => {
    if (!(await hasReadyEditorSession())) {
      throw new Error(
        "No ready Unity Editor session. Open the test project in Unity, wait for compile, then re-run with UCP_EDITOR_INTEGRATION=1",
      );
    }
    await ensureEditMode();
    const play = await runUcpCommand("play", { sessionType: "editor", timeout: 60_000 });
    expect(play.ok, play.message).toBe(true);
    await waitForPlayMode("playing", 60_000);
    await exec(BUILD_PROBE);
  }, 180_000);

  afterAll(async () => {
    await ensureEditMode();
  }, 120_000);

  it("ui_snapshot narrows to a root, lists input fields, and caps its rows", async () => {
    const snapshot = await ok("ui_snapshot", { root: ROOT });
    const canvases = snapshot.canvases as Array<Record<string, unknown>>;
    expect(canvases.map((c) => c.node)).toEqual([ROOT]);

    const [canvas] = canvases;
    const inputs = canvas.inputs as Array<Record<string, unknown>>;
    expect(inputs.map((i) => i.node)).toEqual([`${ROOT}/Name`]);
    const buttons = Object.fromEntries(
      (canvas.buttons as Array<Record<string, unknown>>).map((b) => [b.node, b]),
    );
    expect(buttons[`${ROOT}/Go`]?.interactable).toBe(true);
    expect(buttons[`${ROOT}/Locked`]?.interactable).toBe(false);
    expect(buttons[`${ROOT}/Hidden`]?.active).toBe(false);

    const capped = await ok("ui_snapshot", { root: ROOT, limit: 2 });
    expect(capped.row_count).toBe(2);
    expect(capped.truncated).toBe(true);
    expect(Number(capped.row_count_total)).toBeGreaterThan(2);
  });

  it("ui_click refuses nodes a player could not click", async () => {
    const locked = await rejected("ui_click", { node: `${ROOT}/Locked` });
    expect(locked.error_code).toBe("not_interactable");

    const hidden = await rejected("ui_click", { node: `${ROOT}/Hidden` });
    expect(hidden.error_code).toBe("not_interactable");

    const noHandler = await rejected("ui_click", { node: `${ROOT}/Status` });
    expect(noHandler.error_code).toBe("component_not_found");

    const typo = await rejected("ui_click", { node: `${ROOT}/Gp` });
    expect(typo.error_code).toBe("node_not_found");
    expect(typo.hint).toContain(`'${ROOT}/Go'`);
  });

  it("ui_wait gives up before the caller's timeout and says what it saw", async () => {
    const started = Date.now();
    const waited = await rejected("ui_wait", { node: `${ROOT}/Done` }, 4_000);
    expect(waited.error_code).toBe("timeout");
    expect(waited.message).toContain("exists but is inactive");
    // The agent answered, not ucp-host at the deadline.
    expect(Date.now() - started).toBeLessThan(4_000);

    const missingText = await rejected("ui_wait", { node: ROOT, until: "text" });
    expect(missingText.error_code).toBe("invalid_argument");
  });

  it("ui_input types into a field and ends editing", async () => {
    const typed = await ok("ui_input", { node: `${ROOT}/Name`, text: "ucp", submit: true });
    expect(typed.text).toBe("ucp");
    expect(typed.events).toEqual(["value_changed", "submit", "end_edit"]);

    // Already true: answered without deferring.
    const shown = await ok("ui_wait", { node: `${ROOT}/Status`, text: "ended ucp" });
    expect(shown.text).toBe("ended ucp");
    expect(shown.elapsed_ms).toBe(0);
  });

  it("ui_click on a button's label clicks the button, and ui_wait sees what follows", async () => {
    const clicked = await ok("ui_click", { node: `${ROOT}/Go/Label` });
    expect(clicked.handler).toBe(`${ROOT}/Go`);

    const active = await ok("ui_wait", { node: `${ROOT}/Done` }, 10_000);
    expect(active.until).toBe("active");
    expect(active.active).toBe(true);

    const spawned = await ok("ui_wait", { node: `${ROOT}/Spawned`, until: "exists" }, 10_000);
    expect(spawned.node).toBe(`${ROOT}/Spawned`);

    const label = await ok("ui_wait", { node: ROOT, text: "clicked ucp" });
    expect(label.text).toBe("clicked ucp");

    const cleared = await ok("ui_input", { node: `${ROOT}/Name`, text: "" });
    expect(cleared.text).toBe("");
  });

  it("returns results of the same shape", async () => {
    await expectShapesUnchanged("shapes.ui.json");
  });

  // Last: it leaves Play Mode. The wait outlives the domain reload that stopping causes.
  it("ui_wait fails when Play Mode ends under it", async () => {
    await exec(`${AFTER}\nAfter(0.5, EditorApplication.ExitPlaymode);\nreturn "scheduled";`);
    const waited = await rejected("ui_wait", { node: `${ROOT}/Never`, until: "exists" }, 60_000);
    expect(waited.error_code).toBe("play_mode_required");
    await waitForPlayMode("edit", 60_000);
  }, 90_000);
});

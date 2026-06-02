import { describe, it } from 'node:test';
import assert from 'node:assert/strict';
import { CONNECTOR_HTTP_ERRORS } from '../../src/constants.js';

describe('connector HTTP error contract', () => {
  it('documents single-flight busy response', () => {
    const busy = CONNECTOR_HTTP_ERRORS.SERVER_BUSY;
    assert.equal(busy.status, 503);
    assert.equal(busy.error, 'busy');
  });

  it('documents domain reload response', () => {
    const reloading = CONNECTOR_HTTP_ERRORS.DOMAIN_RELOADING;
    assert.equal(reloading.status, 503);
    assert.equal(reloading.error, 'reloading');
  });
});

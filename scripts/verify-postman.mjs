import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import vm from 'node:vm';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const collection = JSON.parse(fs.readFileSync(path.join(root, 'postman', 'SimpliaDz.postman_collection.json')));
const environment = JSON.parse(fs.readFileSync(path.join(root, 'postman', 'SimpliaDz.local.postman_environment.json')));
const vars = new Map();
const env = new Map(environment.values.map(x => [x.key, x.value]));
env.set('adminPassword', process.env.SIMPLIADZ_POSTMAN_ADMIN_PASSWORD ?? 'Admin123!');
const expectProxy = new Proxy(() => expectProxy, { get: () => expectProxy, apply: () => expectProxy });
const replace = input => input.replace(/\{\{(\w+)\}\}/g, (_, key) =>
  String(vars.get(key) ?? env.get(key) ?? `UNSET_${key}`));
const failures = [];
let total = 0;

function scripts(events, kind, response = null) {
  for (const event of events ?? []) {
    if (event.listen !== kind) continue;
    const pm = {
      collectionVariables: {
        get: key => vars.get(key), set: (key, value) => vars.set(key, value),
        unset: key => vars.delete(key)
      },
      variables: { replaceIn: value => value === '{{$guid}}' ? crypto.randomUUID() : replace(value) },
      response: response ? {
        code: response.status,
        json: () => JSON.parse(response.body), text: () => response.body
      } : undefined,
      test: (_name, callback) => callback(),
      expect: () => expectProxy
    };
    vm.runInNewContext(event.script.exec.join('\n'), { pm, Date }, { timeout: 5000 });
  }
}

async function runItem(item) {
  if (item.item) {
    for (const child of item.item) {
      await runItem(child);
      if (failures.length && process.argv.includes('--stop-on-first')) break;
    }
    return;
  }
  total++;
  try {
    scripts(collection.event, 'prerequest');
    scripts(item.event, 'prerequest');
    const request = item.request;
    const headers = { Accept: 'application/json' };
    if (request.body) headers['Content-Type'] = 'application/json';
    if (request.auth?.type === 'bearer') headers.Authorization =
      `Bearer ${replace(request.auth.bearer[0].value)}`;
    const response = await fetch(replace(request.url), {
      method: request.method, headers,
      body: request.body ? replace(request.body.raw) : undefined,
      signal: AbortSignal.timeout(30000)
    });
    const body = await response.text();
    const expected = JSON.parse(item.event.find(x => x.listen === 'test').script.exec
      .find(x => x.includes('oneOf(')).match(/oneOf\((\[[^)]*\])\)/)[1]);
    if (!expected.includes(response.status)) {
      failures.push(`${item.name}: ${response.status} expected ${expected}; ${body.slice(0, 220)}`);
    } else {
      scripts(item.event, 'test', { status: response.status, body });
    }
  } catch (error) {
    failures.push(`${item.name}: ${error.message}`);
  }
}

for (const folder of collection.item) {
  await runItem(folder);
  if (failures.length && process.argv.includes('--stop-on-first')) break;
}
for (const failure of failures) console.error(failure);
console.log(`${total - failures.length}/${total} requests matched expected status and completed scripts.`);
process.exitCode = failures.length ? 1 : 0;

// Fails when a component has no test, or when a spec skips or isolates tests.
//  - every *.component.ts under src/app needs a sibling *.component.spec.ts (Vitest browser),
//  - except routed pages (src/app/pages/<page>/), which need e2e/pages/<page>.spec.ts (Playwright) instead,
//  - no spec may contain it/describe/test .skip or .only.
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { basename, dirname, join, relative, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const appDir = join(root, 'src', 'app');
const pagesDir = join(appDir, 'pages');
const generatedDir = join(appDir, 'api');

function* walk(dir) {
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const path = join(dir, entry.name);
    if (entry.isDirectory()) {
      if (path !== generatedDir && entry.name !== 'node_modules') yield* walk(path);
    } else {
      yield path;
    }
  }
}

const problems = [];
const show = (path) => relative(root, path).split(sep).join('/');

for (const file of walk(appDir)) {
  if (!file.endsWith('.component.ts')) continue;

  if (file.startsWith(pagesDir + sep)) {
    const page = relative(pagesDir, file).split(sep)[0];
    const e2e = join(root, 'e2e', 'pages', `${page}.spec.ts`);
    if (!existsSync(e2e)) problems.push(`${show(file)}: routed page needs ${show(e2e)}`);
  } else {
    const spec = file.replace(/\.component\.ts$/, '.component.spec.ts');
    if (!existsSync(spec)) problems.push(`${show(file)}: missing ${basename(spec)}`);
  }
}

const skipped = /\b(?:it|describe|test)\s*\.\s*(?:skip|only)\b|\b(?:xit|xdescribe|fit|fdescribe)\s*\(/;
for (const dir of [join(root, 'src'), join(root, 'e2e')]) {
  if (!existsSync(dir)) continue;
  for (const file of walk(dir)) {
    if (file.endsWith('.spec.ts') && skipped.test(readFileSync(file, 'utf8'))) {
      problems.push(`${show(file)}: skipped or focused tests are not allowed`);
    }
  }
}

if (problems.length > 0) {
  console.error(`Test coverage check failed:\n  ${problems.join('\n  ')}`);
  process.exit(1);
}
console.log('Test coverage check passed.');

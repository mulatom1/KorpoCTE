import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { createHash } from 'node:crypto';
import { installTo, uninstallFrom } from '../src/manager.js';

const newProject = (): string => fs.mkdtempSync(path.join(os.tmpdir(), 'tomsoft1-toolkit-'));
const claudeSkill = (dir: string) => path.join(dir, '.claude/skills/code-review/SKILL.md');
const copilotSkill = (dir: string) => path.join(dir, '.github/skills/code-review/SKILL.md');
const claudeRules = (dir: string) => path.join(dir, 'CLAUDE.md');
const copilotRules = (dir: string) => path.join(dir, '.github/copilot-instructions.md');
const manifestFile = (dir: string) => path.join(dir, '.ai-toolkit/manifest.json');
const read = (file: string): string => fs.readFileSync(file, 'utf8');
const digest = (s: string): string => createHash('sha256').update(s).digest('hex');

// Always restore test-created directories.
function withProject(fn: (projectDir: string) => void): void {
  const projectDir = newProject();
  try { return fn(projectDir); }
  finally { fs.rmSync(projectDir, { recursive: true, force: true }); }
}

test('installs a shared skill and independent instruction blocks for Claude and Copilot', () => withProject((dir) => {
  fs.mkdirSync(path.join(dir, '.github'), { recursive: true });
  fs.writeFileSync(claudeRules(dir), '# Moje reguły Claude\n');
  fs.writeFileSync(copilotRules(dir), '# Moje reguły Copilot\n');
  installTo(dir);
  assert.equal(read(claudeSkill(dir)), read(copilotSkill(dir)));
  assert.match(read(claudeSkill(dir)), /name: code-review/);
  assert.match(read(claudeRules(dir)), /Moje reguły Claude/);
  assert.match(read(copilotRules(dir)), /Moje reguły Copilot/);
  assert.equal((read(claudeRules(dir)).match(/ai-toolkit:start/g) ?? []).length, 1);
  assert.equal((read(copilotRules(dir)).match(/ai-toolkit:start/g) ?? []).length, 1);
  const manifest = JSON.parse(read(manifestFile(dir)));
  assert.equal(manifest.schemaVersion, 2);
  assert.equal(manifest.skills.length, 2);
  assert.equal(manifest.rules.length, 2);
}));

test('repeat install is idempotent and uninstall retains unrelated instructions', () => withProject((dir) => {
  fs.mkdirSync(path.join(dir, '.github'), { recursive: true });
  fs.writeFileSync(claudeRules(dir), '# Claude użytkownika\n');
  fs.writeFileSync(copilotRules(dir), '# Copilot użytkownika\n');
  installTo(dir);
  const claudeBefore = read(claudeRules(dir));
  const copilotBefore = read(copilotRules(dir));
  installTo(dir);
  assert.equal(read(claudeRules(dir)), claudeBefore);
  assert.equal(read(copilotRules(dir)), copilotBefore);
  uninstallFrom(dir);
  assert.ok(!fs.existsSync(claudeSkill(dir)));
  assert.ok(!fs.existsSync(copilotSkill(dir)));
  assert.equal(read(claudeRules(dir)), '# Claude użytkownika\n');
  assert.equal(read(copilotRules(dir)), '# Copilot użytkownika\n');
  assert.ok(!fs.existsSync(manifestFile(dir)));
}));

test('uninstall deletes created instructions when they had no unrelated content', () => withProject((dir) => {
  installTo(dir);
  uninstallFrom(dir);
  assert.ok(!fs.existsSync(claudeRules(dir)));
  assert.ok(!fs.existsSync(copilotRules(dir)));
}));

test('modified skill prevents both reinstall and uninstall, no other target modified', () => withProject((dir) => {
  installTo(dir);
  const copilotBefore = read(copilotSkill(dir));
  fs.appendFileSync(claudeSkill(dir), '\nMój dopisek\n');
  assert.throws(() => installTo(dir), /Zmodyfikowany lub niezarządzany skill/);
  assert.throws(() => uninstallFrom(dir), /Skill zmodyfikowano ręcznie/);
  assert.equal(read(copilotSkill(dir)), copilotBefore);
  assert.ok(fs.existsSync(manifestFile(dir)));
}));

test('edited Copilot skill prevents removal of Claude skill, rules or manifest', () => withProject((dir) => {
  installTo(dir);
  fs.appendFileSync(copilotSkill(dir), '\nSpecjalne instrukcje\n');
  assert.throws(() => uninstallFrom(dir), /Skill zmodyfikowano ręcznie/);
  assert.ok(fs.existsSync(claudeSkill(dir)));
  assert.ok(fs.existsSync(claudeRules(dir)));
  assert.ok(fs.existsSync(manifestFile(dir)));
}));

test('edited Copilot instruction block cannot be replaced or removed', () => withProject((dir) => {
  installTo(dir);
  const before = read(claudeRules(dir));
  fs.writeFileSync(copilotRules(dir), read(copilotRules(dir)).replace('Zanim edytujesz', 'Zawsze zanim edytujesz'));
  assert.throws(() => installTo(dir), /Zmodyfikowane reguły/);
  assert.throws(() => uninstallFrom(dir), /Reguły .* zmodyfikowano ręcznie/);
  assert.equal(read(claudeRules(dir)), before);
  assert.ok(fs.existsSync(claudeSkill(dir)));
}));

test('refuses unmanaged conflicting skill and keeps filesystem unchanged', () => withProject((dir) => {
  fs.mkdirSync(path.dirname(copilotSkill(dir)), { recursive: true });
  fs.writeFileSync(copilotSkill(dir), '# Inny skill\n');
  assert.throws(() => installTo(dir), /Zmodyfikowany lub niezarządzany skill/);
  assert.ok(!fs.existsSync(claudeSkill(dir)));
  assert.ok(!fs.existsSync(manifestFile(dir)));
  assert.equal(read(copilotSkill(dir)), '# Inny skill\n');
}));

test('refuses conflicting marker block in pre-existing Copilot instructions', () => withProject((dir) => {
  fs.mkdirSync(path.dirname(copilotRules(dir)), { recursive: true });
  fs.writeFileSync(copilotRules(dir), '<!-- ai-toolkit:start -->\nCudze reguły\n<!-- ai-toolkit:end -->\n');
  assert.throws(() => installTo(dir), /Niezarządzane lub ręcznie zmodyfikowane reguły/);
  assert.ok(!fs.existsSync(claudeSkill(dir)));
  assert.ok(!fs.existsSync(manifestFile(dir)));
}));

test('migration from 0.1.0 preserves Claude skill and adds Copilot targets', () => withProject((dir) => {
  installTo(dir);
  // Simulate the old on-disk v0.1.0 layout and manifest.
  fs.unlinkSync(copilotSkill(dir));
  fs.unlinkSync(copilotRules(dir));
  const skillData = read(claudeSkill(dir));
  fs.writeFileSync(manifestFile(dir), JSON.stringify({
    managedBy: 'ai-toolkit',
    skillPath: '.claude/skills/code-review/SKILL.md',
    skillSha256: digest(skillData),
    rulesPath: 'CLAUDE.md',
  }, null, 2));
  installTo(dir);
  assert.ok(fs.existsSync(copilotSkill(dir)));
  assert.ok(fs.existsSync(copilotRules(dir)));
  assert.equal(JSON.parse(read(manifestFile(dir))).schemaVersion, 2);
  uninstallFrom(dir);
  assert.ok(!fs.existsSync(claudeSkill(dir)));
  assert.ok(!fs.existsSync(copilotSkill(dir)));
}));

test('invalid manifest never allows overwriting or removal', () => withProject((dir) => {
  fs.mkdirSync(path.dirname(manifestFile(dir)), { recursive: true });
  fs.writeFileSync(manifestFile(dir), '{ bad json');
  assert.throws(() => installTo(dir), /Nieprawidłowy manifest/);
  assert.throws(() => uninstallFrom(dir), /Nieprawidłowy manifest/);
  assert.ok(!fs.existsSync(claudeSkill(dir)));
}));


test('legacy uninstall refuses to discard edits made to CLAUDE.md managed block', () => withProject((dir) => {
  installTo(dir);
  const skillData = read(claudeSkill(dir));
  fs.writeFileSync(manifestFile(dir), JSON.stringify({
    managedBy: 'ai-toolkit',
    skillPath: '.claude/skills/code-review/SKILL.md',
    skillSha256: digest(skillData),
    rulesPath: 'CLAUDE.md',
  }, null, 2));
  fs.writeFileSync(claudeRules(dir), read(claudeRules(dir)).replace('Zanim edytujesz', 'Zanim zmienisz'));
  assert.throws(() => uninstallFrom(dir), /Reguły .* zmodyfikowano ręcznie/);
  assert.ok(fs.existsSync(claudeSkill(dir)));
  assert.ok(fs.existsSync(manifestFile(dir)));
}));

test('uninstallation preserves instructions after managed block', () => withProject((dir) => {
  installTo(dir);
  fs.appendFileSync(copilotRules(dir), '\n# Instrukcje po bloku\n');
  uninstallFrom(dir);
  assert.match(read(copilotRules(dir)), /Instrukcje po bloku/);
  assert.ok(!fs.existsSync(claudeSkill(dir)));
}));

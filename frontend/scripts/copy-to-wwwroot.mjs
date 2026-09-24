#!/usr/bin/env node
// Copies the compiled Angular output into backend/GhumoOdisha.Api/wwwroot
// without touching wwwroot/uploads (runtime-uploaded trip/room/vehicle photos).
// Reads angular.json instead of assuming a dist path, since the
// @angular/build:application builder nests browser output under "browser/".
import { existsSync, mkdirSync, readdirSync, rmSync, cpSync, readFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const frontendDir = join(dirname(fileURLToPath(import.meta.url)), '..');

const angularJson = JSON.parse(readFileSync(join(frontendDir, 'angular.json'), 'utf8'));
const projectName = Object.keys(angularJson.projects)[0];
const buildOptions = angularJson.projects[projectName]?.architect?.build?.options ?? {};
const configuredOutputPath = buildOptions.outputPath;
const baseOutputPath =
  typeof configuredOutputPath === 'string'
    ? configuredOutputPath
    : (configuredOutputPath?.base ?? join('dist', projectName));

let distDir = join(frontendDir, baseOutputPath);
if (existsSync(join(distDir, 'browser'))) {
  distDir = join(distDir, 'browser');
}

if (!existsSync(join(distDir, 'index.html'))) {
  console.error(`Could not find a built Angular app (index.html) under ${distDir}. Run "ng build" first.`);
  process.exit(1);
}

const wwwrootDir = join(frontendDir, '..', 'backend', 'GhumoOdisha.Api', 'wwwroot');
mkdirSync(wwwrootDir, { recursive: true });

for (const entry of readdirSync(wwwrootDir)) {
  if (entry === 'uploads' || entry === '.gitignore') continue;
  rmSync(join(wwwrootDir, entry), { recursive: true, force: true });
}

cpSync(distDir, wwwrootDir, { recursive: true });

console.log(`Copied Angular build from ${distDir}`);
console.log(`                    to ${wwwrootDir}`);

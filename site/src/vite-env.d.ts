/// <reference types="vite/client" />

// MOD34: read at build time by vite.config.ts.
declare const __RELEASE__: { version: string; installer: string; url: string; bytes: number } | null;
declare const __MODELS__: { name: string; task: string; bytes: number; license: string; licenseUrl: string }[];

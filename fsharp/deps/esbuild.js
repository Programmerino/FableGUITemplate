require('esbuild').build({
    entryPoints: [process.argv[2]],
    bundle: true,
    outdir: process.env.out,
    format: 'esm',
    platform: 'browser',
    target: 'esnext',
    treeShaking: true,
    external: ["path", "fs", "url", "perf_hooks", "os", "readline", "worker_threads"],
    define: { "global": "window" }
}).catch(() => process.exit(1))
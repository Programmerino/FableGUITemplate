import { defineConfig } from 'vite';
import crossOriginIsolation from 'vite-plugin-cross-origin-isolation';
import filterReplace from 'vite-plugin-filter-replace';

export default defineConfig({
    define: {
        "global": "globalThis",
    },
    cacheDir: ".vite",
    preserveSymlinks: true,
    plugins: [
        crossOriginIsolation(),
        filterReplace.default(
            [
                {
                    filter: /\.worker.js$/,
                    replace: {
                        from: "e.data.urlOrBlob?import(e.data.urlOrBlob):",
                        to: ""
                    }
                }
            ]
        )
    ],
    optimizeDeps: {
        esbuildOptions: {
            define: {
                global: 'globalThis'
            },
        },
        exclude: ['/home/davis/Documents/Personal/CSProjects/FableGUITemplate/dev/js/app.fut.worker.js'],
    },
});
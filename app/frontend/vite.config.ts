import path from "node:path";
import { fileURLToPath } from "node:url";
import { defineConfig } from "vite";
import vue from "@vitejs/plugin-vue";

const __dirname = fileURLToPath(new URL(".", import.meta.url));

export default defineConfig({
    plugins: [vue()],
    resolve: {
        alias: {
            "@": path.resolve(__dirname, "./src"),
        },
    },
    server: {
        proxy: {
            "/api": { target: "http://127.0.0.1:5000", changeOrigin: true },
            "/static/pictures": { target: "http://127.0.0.1:5000", changeOrigin: true },
        },
    },
});

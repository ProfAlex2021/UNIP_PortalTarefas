/// <reference types="vitest/config" />
import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";

// https://vite.dev/config/
export default defineConfig(({ mode, command }) => {
  // Variáveis SEM o prefixo VITE_ ficam só no Node (configuração do servidor de desenvolvimento)
  // e não são embutidas no JavaScript enviado ao navegador.
  const env = loadEnv(mode, process.cwd(), "");
  const alvoApi = env.API_PROXY_TARGET || "https://localhost:7260";

  return {
    // Roteiro 8: no build de produção a SPA é servida pela API em /app/ (implantação integrada).
    // Em desenvolvimento continua na raiz de http://localhost:5173.
    base: command === "build" ? "/app/" : "/",
    plugins: [react()],
    server: {
      port: 5173,
      strictPort: true,
      // Proxy de desenvolvimento: o navegador chama http://localhost:5173/api/... (mesma origem)
      // e o Vite repassa para a API. Assim não há CORS nem cookies entre origens.
      proxy: {
        "/api": {
          target: alvoApi,
          changeOrigin: true,
          secure: false, // certificado HTTPS de desenvolvimento do .NET (autoassinado)
        },
      },
    },
    test: {
      environment: "jsdom",
      setupFiles: ["./src/testes/setup.ts"],
      css: false,
    },
  };
});

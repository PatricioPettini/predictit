/// <reference types="vitest/config" />
import { defineConfig } from 'vitest/config';
import { loadEnv } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig(({ mode }) => {
  // Prefijo vacio: se leen tambien las variables sin `VITE_`. Esta no la
  // consume el navegador sino el servidor de desarrollo, asi que no tiene por
  // que quedar expuesta en el bundle.
  const entorno = loadEnv(mode, '.', '');

  return {
    plugins: [react()],
    server: {
      port: 5173,
      // El proxy evita CORS en desarrollo y, sobre todo, evita que la URL de
      // la API quede escrita en el codigo del frontend.
      //
      // El destino sale del entorno porque hay dos formas de tener la API
      // arriba: `dotnet run`, que escucha en 5099, y el contenedor de
      // `docker compose`, que publica el 8081. Sin esto habia que editar este
      // archivo para pasar de una a la otra, y el cambio terminaba commiteado.
      proxy: {
        '/api': {
          target: entorno.PREDICTIT_API || 'http://localhost:5099',
          changeOrigin: true,
        },
      },
    },
    test: {
      environment: 'jsdom',
      globals: true,
      setupFiles: './src/pruebas/preparar.ts',
    },
  };
});

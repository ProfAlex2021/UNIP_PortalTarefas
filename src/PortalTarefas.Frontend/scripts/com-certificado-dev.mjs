// Executa um comando confiando no certificado HTTPS de desenvolvimento do .NET.
// O Node não usa o repositório de certificados do Windows; por isso exportamos apenas
// o certificado PÚBLICO (sem chave privada) e o informamos via NODE_EXTRA_CA_CERTS.
// Uso: node scripts/com-certificado-dev.mjs <comando> [argumentos...]
import { execFileSync, spawnSync } from "node:child_process";
import { existsSync, mkdirSync } from "node:fs";
import { resolve } from "node:path";

const certificado = resolve(".cert", "localhost.pem");

if (!existsSync(certificado)) {
  mkdirSync(".cert", { recursive: true });
  execFileSync("dotnet", ["dev-certs", "https", "--export-path", certificado, "--format", "Pem"], { stdio: "inherit" });
}

const [comando, ...argumentos] = process.argv.slice(2);
const resultado = spawnSync(comando, argumentos, {
  stdio: "inherit",
  shell: true,
  env: { ...process.env, NODE_EXTRA_CA_CERTS: certificado },
});
process.exit(resultado.status ?? 1);

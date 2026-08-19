import { defineConfig, devices } from "@playwright/test";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(here, "../..");

const WEB_PORT = Number(process.env.E2E_WEB_PORT ?? 5173);
const API_PORT = Number(process.env.E2E_API_PORT ?? 5080);

export const WEB_URL = process.env.E2E_BASE_URL ?? `http://localhost:${WEB_PORT}`;
export const API_URL = process.env.E2E_API_URL ?? `http://localhost:${API_PORT}`;

export default defineConfig({
  testDir: "./tests",
  outputDir: "./test-results",
  fullyParallel: false,
  workers: 1,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 1 : 0,
  timeout: 90_000,
  expect: { timeout: 20_000 },
  reporter: process.env.CI
    ? [["list"], ["html", { open: "never" }], ["junit", { outputFile: "test-results/junit.xml" }]]
    : [["list"]],
  use: {
    baseURL: WEB_URL,
    actionTimeout: 20_000,
    navigationTimeout: 30_000,
    trace: "retain-on-failure",
    video: "retain-on-failure",
    screenshot: "only-on-failure",
    locale: "tr-TR",
    timezoneId: "Europe/Istanbul",
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
  webServer: [
    {
      command: `dotnet run --no-launch-profile --project "${path.join(repoRoot, "src", "TakeAuction.Api", "TakeAuction.Api.csproj")}"`,
      cwd: repoRoot,
      url: `${API_URL}/health/ready`,
      reuseExistingServer: !process.env.CI,
      timeout: 300_000,
      stdout: "pipe",
      stderr: "pipe",
      env: {
        ...process.env,
        ASPNETCORE_ENVIRONMENT: "Development",
        ASPNETCORE_URLS: API_URL,
        RateLimiting__PermitLimit: "1000000",
        RateLimiting__AuthPermitLimit: "1000000",
      },
    },
    {
      command: `npm run dev -- --port ${WEB_PORT} --strictPort`,
      cwd: path.join(repoRoot, "src", "TakeAuction.Web"),
      url: WEB_URL,
      reuseExistingServer: !process.env.CI,
      timeout: 180_000,
      stdout: "pipe",
      stderr: "pipe",
    },
  ],
});

import { http } from "./client";
import type { AuthenticatedUser, CurrentUser, UserRole } from "./types";

export async function login(email: string, password: string): Promise<AuthenticatedUser> {
  const { data } = await http.post<AuthenticatedUser>("/auth/login", { email, password });
  return data;
}

export async function register(payload: {
  email: string;
  displayName: string;
  password: string;
  role: Exclude<UserRole, "Admin">;
}): Promise<AuthenticatedUser> {
  const { data } = await http.post<AuthenticatedUser>("/auth/register", payload);
  return data;
}

export async function logout(): Promise<void> {
  await http.post("/auth/logout");
}

export async function getCurrentUser(): Promise<CurrentUser | null> {
  const { status, data } = await http.get<CurrentUser>("/auth/me");
  return status === 204 ? null : data;
}

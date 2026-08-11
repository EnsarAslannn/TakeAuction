import { create } from "zustand";
import * as authApi from "@/api/auth";
import { toApiError } from "@/api/client";
import { auctionHub } from "@/realtime/hub";
import type { CurrentUser, UserRole } from "@/api/types";

interface AuthState {
  user: CurrentUser | null;
  status: "idle" | "loading" | "ready";
  error: string | null;
  bootstrap: () => Promise<void>;
  login: (email: string, password: string) => Promise<void>;
  register: (payload: {
    email: string;
    displayName: string;
    password: string;
    role: Exclude<UserRole, "Admin">;
  }) => Promise<void>;
  logout: () => Promise<void>;
  clearError: () => void;
}

export const useAuthStore = create<AuthState>((set) => ({
  user: null,
  status: "idle",
  error: null,

  bootstrap: async () => {
    set({ status: "loading" });
    try {
      const user = await authApi.getCurrentUser();
      set({ user, status: "ready", error: null });
    } catch {
      set({ user: null, status: "ready" });
    }
  },

  login: async (email, password) => {
    set({ error: null });
    try {
      await authApi.login(email, password);
      const user = await authApi.getCurrentUser();
      set({ user, status: "ready" });
      await auctionHub.reset();
    } catch (error) {
      const apiError = toApiError(error);
      set({ error: apiError.message });
      throw apiError;
    }
  },

  register: async (payload) => {
    set({ error: null });
    try {
      await authApi.register(payload);
      const user = await authApi.getCurrentUser();
      set({ user, status: "ready" });
      await auctionHub.reset();
    } catch (error) {
      const apiError = toApiError(error);
      set({ error: apiError.message });
      throw apiError;
    }
  },

  logout: async () => {
    try {
      await authApi.logout();
    } finally {
      set({ user: null, error: null });
      await auctionHub.reset();
    }
  },

  clearError: () => set({ error: null }),
}));

export const canSell = (user: CurrentUser | null) => user?.role === "Seller" || user?.role === "Admin";

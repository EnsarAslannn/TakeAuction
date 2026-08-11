import type { Config } from "tailwindcss";

export default {
  content: ["./index.html", "./src/**/*.{ts,tsx}"],
  theme: {
    extend: {
      colors: {
        ink: { DEFAULT: "#1A1815", soft: "#2C2823", muted: "#52443D" },
        paper: { DEFAULT: "#F4F1EB", pure: "#FBFAF7", warm: "#EFE3D2" },
        sand: { DEFAULT: "#C0A070", deep: "#A0805F", pale: "#E0C6A0" },
        slate: { DEFAULT: "#7E9BBD", pale: "#B0C4DA", deep: "#5E7A9B" },
        stone: { DEFAULT: "#8B837C", light: "#C4B1A4", dark: "#635C56" },
      },
      fontFamily: {
        display: ["Bricolage Grotesque", "Inter Tight", "system-ui", "sans-serif"],
        sans: ["Inter Tight", "Inter", "system-ui", "sans-serif"],
        mono: ["IBM Plex Mono", "ui-monospace", "monospace"],
      },
      letterSpacing: {
        tightest: "-0.055em",
        headline: "-0.04em",
      },
      fontSize: {
        mega: ["clamp(3.5rem, 13vw, 15rem)", { lineHeight: "0.82", letterSpacing: "-0.05em" }],
        giant: ["clamp(2.6rem, 8vw, 8rem)", { lineHeight: "0.88", letterSpacing: "-0.045em" }],
        huge: ["clamp(2rem, 5vw, 4.5rem)", { lineHeight: "0.95", letterSpacing: "-0.035em" }],
        eyebrow: ["0.6875rem", { lineHeight: "1", letterSpacing: "0.22em" }],
      },
      maxWidth: { shell: "96rem" },
      transitionTimingFunction: {
        editorial: "cubic-bezier(0.16, 1, 0.3, 1)",
      },
      keyframes: {
        "veil-up": {
          from: { transform: "translate3d(0, 110%, 0)" },
          to: { transform: "translate3d(0, 0, 0)" },
        },
        drift: {
          "0%, 100%": { transform: "translate3d(0, 0, 0)" },
          "50%": { transform: "translate3d(0, -10px, 0)" },
        },
        shimmer: {
          "0%": { backgroundPosition: "-200% 0" },
          "100%": { backgroundPosition: "200% 0" },
        },
      },
      animation: {
        "veil-up": "veil-up 1s cubic-bezier(0.16, 1, 0.3, 1) forwards",
        drift: "drift 6s ease-in-out infinite",
        shimmer: "shimmer 2.4s linear infinite",
      },
    },
  },
  plugins: [],
} satisfies Config;

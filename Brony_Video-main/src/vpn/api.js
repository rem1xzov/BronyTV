import { apiFetch } from "../auth/api";

export const fetchVpnStatus = () =>
  apiFetch("/vpn/status", { method: "GET" });

export const startVpnTrial = () =>
  apiFetch("/vpn/trial", { method: "POST" });

export const activateVpnPromo = (code) =>
  apiFetch("/vpn/promo", {
    method: "POST",
    body: JSON.stringify({ code })
  });

export const reviveVpn = () =>
  apiFetch("/vpn/revoke", { method: "POST" });

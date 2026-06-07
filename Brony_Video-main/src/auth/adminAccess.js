const FALLBACK_PRIVILEGED_USERNAMES = new Set(["rainbowdash"]);

export function isPlatformAdmin(user) {
  if (!user) {
    return false;
  }

  if (user.isPlatformAdmin === true || user.isOwner === true) {
    return true;
  }

  const role = user.platformRole?.trim();
  if (role === "Admin" || role === "Owner") {
    return true;
  }

  const username = user.username?.trim().toLowerCase();
  return Boolean(username && FALLBACK_PRIVILEGED_USERNAMES.has(username));
}

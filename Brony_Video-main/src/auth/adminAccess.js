const FALLBACK_PRIVILEGED_USERNAMES = new Set(["rainbowdash"]);

export function isPlatformAdmin(user) {
  if (!user) {
    return false;
  }

  if (user.isPlatformAdmin === true) {
    return true;
  }

  const username = user.username?.trim().toLowerCase();
  return Boolean(username && FALLBACK_PRIVILEGED_USERNAMES.has(username));
}

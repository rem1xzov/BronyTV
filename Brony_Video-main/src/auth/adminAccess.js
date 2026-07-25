const FALLBACK_PRIVILEGED_USERNAMES = new Set(["rainbowdash"]); // Твой резервный список

export function isPlatformAdmin(user) {
  if (!user) {
    return false;
  }

  if (user.isPlatformAdmin === true || user.isOwner === true) {
    return true;
  }

  const rawRole = user.platformRole || user.role;
  if (rawRole) {
    const role = rawRole.trim().toLowerCase();
    if (role === "admin" || role === "owner") {
      return true;
    }
  }


  const username = user.username?.trim().toLowerCase();
  return Boolean(username && FALLBACK_PRIVILEGED_USERNAMES.has(username));
}

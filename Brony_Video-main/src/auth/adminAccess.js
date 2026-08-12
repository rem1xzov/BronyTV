export function isPlatformAdmin(user) {
  if (!user) {
    return false;
  }

  if (user.isPlatformAdmin === true || user.isOwner === true) {
    return true;
  }

  const rawRole = user.platformRole || user.role;
  if (!rawRole) {
    return false;
  }

  const role = rawRole.trim().toLowerCase();
  return role === "admin" || role === "owner";
}

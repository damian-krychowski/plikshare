namespace PlikShare.Core.Authorization;

public class Roles
{
    public const string Admin = "admin";
    public const string AdminNormalized = "ADMIN";
   

    public static bool IsValidRole(string role) => role == Admin;
}
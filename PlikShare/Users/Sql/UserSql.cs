namespace PlikShare.Users.Sql;

public static class UserSql
{
    public static string HasRole(string roleName)
    {
        return $@"
            EXISTS (
                SELECT 1
                FROM ur_user_roles
                INNER JOIN r_roles
                    ON r_id = ur_role_id
                WHERE 
                    ur_user_id = u_id
                    AND r_name = '{roleName}'
            )";
    }

    public static string HasClaim(string type, string value)
    {
        return $@"
            EXISTS (
                SELECT 1
                FROM uc_user_claims
                WHERE 
                    uc_user_id = u_id
                    AND uc_claim_type = '{type}'
                    AND uc_claim_value = '{value}'
            )
        ";
    }
}
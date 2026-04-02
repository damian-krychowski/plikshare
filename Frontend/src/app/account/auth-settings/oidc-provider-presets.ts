export interface OidcProviderPreset {
    key: string;
    name: string;
    issuerUrl: string;
    issuerUrlPlaceholder: string;
    instructions: string[];
}

export const OIDC_PROVIDER_PRESETS: Record<string, OidcProviderPreset> = {
    'google': {
        key: 'google',
        name: 'Google',
        issuerUrl: 'https://accounts.google.com',
        issuerUrlPlaceholder: 'https://accounts.google.com',
        instructions: [
            'Go to <strong>Google Cloud Console</strong> → APIs & Services → Credentials',
            'Create <strong>OAuth 2.0 Client ID</strong> (Web application)',
            'Set <strong>Authorized redirect URI</strong> to: <code>{redirectUri}</code>',
            'Copy <strong>Client ID</strong> and <strong>Client Secret</strong>'
        ]
    },
    'microsoft': {
        key: 'microsoft',
        name: 'Microsoft',
        issuerUrl: 'https://login.microsoftonline.com/{tenant-id}/v2.0',
        issuerUrlPlaceholder: 'https://login.microsoftonline.com/{tenant-id}/v2.0',
        instructions: [
            'Go to <strong>Entra ID</strong> (Azure AD) → App Registrations → New Registration',
            'Set <strong>Redirect URI</strong> (Web) to: <code>{redirectUri}</code>',
            'Go to Certificates & Secrets → <strong>New client secret</strong>',
            'Copy <strong>Application (client) ID</strong> and the <strong>secret value</strong>',
            'Replace <code>{tenant-id}</code> in Issuer URL with your <strong>Directory (tenant) ID</strong>'
        ]
    },
    'keycloak': {
        key: 'keycloak',
        name: 'Keycloak',
        issuerUrl: 'https://{host}/realms/{realm}',
        issuerUrlPlaceholder: 'https://{host}/realms/{realm}',
        instructions: [
            'Go to <strong>Keycloak Admin Console</strong> → your realm → Clients → Create client',
            'Client type: <strong>OpenID Connect</strong>',
            'Enable <strong>Client Authentication</strong> (confidential)',
            'Set <strong>Valid redirect URIs</strong> to: <code>{redirectUri}</code>',
            'Go to Credentials tab, copy the <strong>Client Secret</strong>',
            'Set <strong>Issuer URL</strong> to: <code>https://{host}/realms/{realm}</code>'
        ]
    },
    'okta': {
        key: 'okta',
        name: 'Okta',
        issuerUrl: 'https://{domain}.okta.com',
        issuerUrlPlaceholder: 'https://{domain}.okta.com',
        instructions: [
            'Go to <strong>Okta Admin</strong> → Applications → Create App Integration',
            'Sign-in method: <strong>OIDC</strong>, Application type: <strong>Web Application</strong>',
            'Set <strong>Sign-in redirect URI</strong> to: <code>{redirectUri}</code>',
            'Copy <strong>Client ID</strong> and <strong>Client Secret</strong>'
        ]
    },
    'auth0': {
        key: 'auth0',
        name: 'Auth0',
        issuerUrl: 'https://{domain}.auth0.com/',
        issuerUrlPlaceholder: 'https://{domain}.auth0.com/',
        instructions: [
            'Go to <strong>Auth0 Dashboard</strong> → Applications → Create Application',
            'Choose <strong>Regular Web Applications</strong>',
            'Set <strong>Allowed Callback URLs</strong> to: <code>{redirectUri}</code>',
            'Copy <strong>Client ID</strong> and <strong>Client Secret</strong> from Settings tab'
        ]
    },
    'custom': {
        key: 'custom',
        name: 'Custom OIDC',
        issuerUrl: '',
        issuerUrlPlaceholder: 'https://keycloak.example.com/realms/main',
        instructions: [
            'In your identity provider, create a new <strong>OIDC client/application</strong>',
            'Set the <strong>Redirect URI</strong> to: <code>{redirectUri}</code>',
            'Enable <strong>Client Authentication</strong> (confidential client)',
            'Copy the <strong>Client ID</strong> and <strong>Client Secret</strong> below'
        ]
    }
};

export const OIDC_PRESET_ORDER: string[] = ['google', 'microsoft', 'keycloak', 'okta', 'auth0', 'custom'];

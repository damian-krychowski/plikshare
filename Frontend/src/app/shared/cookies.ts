import { XSRF_TOKEN_COOKIE_NAME } from "./xsrf";

export class CookieUtils {
    public static getValue(cookieName: string): string {
        const match = document.cookie.match('(^|;)\\s*' + cookieName + '\\s*=\\s*([^;]+)');
        return match ? match.pop() || '' : '';
    }

    public static GetXsrfToken() {
        return this.getValue(XSRF_TOKEN_COOKIE_NAME);
    }
}
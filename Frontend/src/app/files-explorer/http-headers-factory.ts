export type HttpHeadersFactory = {
    prepareAdditionalHttpHeaders: () => Record<string, string> | undefined;
}
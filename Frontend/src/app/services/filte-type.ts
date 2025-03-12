export type FileType = 'image' | 'video' | 'other' | 'pdf' | 'audio' | 'text' | 'archive' | 'markdown';

export type FileDetails = {
    type: FileType;
    isPreviewable: boolean;
}

export function getFileDetails(fileExtension: string): FileDetails {
    const fileTypes: Record<FileType, string[]> = {
        image: ['.jpg', '.jpeg', '.png', '.gif', '.bmp', '.webp', '.svg'],
        video: ['.mp4', '.mov', '.webm', '.ogg', '.m4v', '.mkv'],
        pdf: ['.pdf'],
        other: [],
        archive: ['.zip'],
        audio: ['.mp3', '.wav', '.ogg', '.m4a', '.aac'],
        text: [
            '.txt', '.csv', '.json', '.xml', '.html', '.js', '.css', '.py', '.java', '.cs', 
            '.ts', '.scss', '.yml', '.sh', '.sln', '.ps1', 
            '.rb', '.go', '.php', '.sql', '.r', '.c', '.cpp', '.h', '.swift', '.kt', '.rs', '.lua', '.pl',
            '.ini', '.env', '.toml', '.conf', '.properties', '.yaml', '.lock', '.gitignore', '.editorconfig',
            '.graphql', '.proto', '.rst', '.tex', '.adoc'
        ],
        markdown: ['.md']
    };

    const fileType = (Object.entries(fileTypes).find(([_, extensions]) => extensions.includes(fileExtension))?.[0] as FileType) || 'other';

    return {
        type: fileType,
        isPreviewable: fileType != 'other'
    };
}

export function getMimeType(fileExtension: string | null): string {
    if(!fileExtension)
        return 'application/octet-stream';

    const mimeTypes: Record<string, string> = {
        // Images
        '.jpg': 'image/jpeg',
        '.jpeg': 'image/jpeg',
        '.png': 'image/png', 
        '.gif': 'image/gif',
        '.webp': 'image/webp',
        '.bmp': 'image/bmp',
        '.svg': 'image/svg+xml',
        '.tiff': 'image/tiff',
        '.ico': 'image/x-icon',
        
        // Audio
        '.mp3': 'audio/mpeg',
        '.wav': 'audio/wav',
        '.ogg': 'audio/ogg',
        '.m4a': 'audio/mp4',
        '.aac': 'audio/aac',
        '.flac': 'audio/flac',
        '.wma': 'audio/x-ms-wma',

        // Video
        '.mp4': 'video/mp4',
        '.webm': 'video/webm',
        '.avi': 'video/x-msvideo',
        '.mov': 'video/quicktime',
        '.wmv': 'video/x-ms-wmv',
        '.flv': 'video/x-flv',
        '.mkv': 'video/x-matroska',

        // Documents
        '.txt': 'text/plain',
        '.csv': 'text/csv',
        '.json': 'application/json',
        '.xml': 'application/xml',
        '.md': 'text/markdown',
        '.html': 'text/html',
        '.htm': 'text/html',
        '.pdf': 'application/pdf',
        '.doc': 'application/msword',
        '.docx': 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
        '.xls': 'application/vnd.ms-excel',
        '.xlsx': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
        '.ppt': 'application/vnd.ms-powerpoint',
        '.pptx': 'application/vnd.openxmlformats-officedocument.presentationml.presentation',
        '.odt': 'application/vnd.oasis.opendocument.text',
        '.ods': 'application/vnd.oasis.opendocument.spreadsheet',
        '.odp': 'application/vnd.oasis.opendocument.presentation',

        // Archives
        '.zip': 'application/zip',
        '.rar': 'application/x-rar-compressed',
        '.7z': 'application/x-7z-compressed',
        '.tar': 'application/x-tar',
        '.gz': 'application/gzip',

        // Programming
        '.js': 'application/javascript',
        '.mjs': 'application/javascript',
        '.css': 'text/css',
        '.py': 'text/x-python',
        '.java': 'text/x-java-source',
        '.cpp': 'text/x-c++src',
        '.cs': 'text/x-csharp',
        '.php': 'application/x-httpd-php',
        '.rb': 'text/x-ruby',
        '.swift': 'text/x-swift',
        '.ts': 'application/typescript',
        '.jsx': 'text/jsx',
        '.tsx': 'text/tsx',

        // Fonts
        '.ttf': 'font/ttf',
        '.otf': 'font/otf',
        '.woff': 'font/woff',
        '.woff2': 'font/woff2',
        '.eot': 'application/vnd.ms-fontobject'
    };

    return mimeTypes[fileExtension.toLowerCase()] || 'application/octet-stream';
}

export function toNameAndExtension(fileName: string): { name: string, extension: string } {
    // Throw error for empty string
    if (!fileName) {
        throw new Error('Filename cannot be empty');
    }

    // Handle dot files (files starting with .)
    if (fileName.startsWith('.') && !fileName.substring(1).includes('.')) {
        return {
            name: fileName,
            extension: ''
        };
    }

    const lastDotIndex = fileName.lastIndexOf('.');
    
    // If no dot found or dot is the first character
    if (lastDotIndex <= 0) {
        return {
            name: fileName,
            extension: ''
        };
    }

    return {
        name: fileName.substring(0, lastDotIndex),
        extension: fileName.substring(lastDotIndex)
    };
}

/**
 * Maps language identifiers commonly used in code blocks to their corresponding file extensions
 * @param language The language identifier
 * @returns The file extension with leading dot (e.g., '.js')
 */
export function getExtensionFromLanguage(language: string): string {
    const languageToExtension: Record<string, string> = {
        'typescript': '.ts',
        'javascript': '.js',
        'python': '.py',
        'html': '.html',
        'css': '.css',
        'json': '.json',
        'java': '.java',
        'csharp': '.cs',
        'c': '.c',
        'cpp': '.cpp',
        'go': '.go',
        'rust': '.rs',
        'ruby': '.rb',
        'php': '.php',
        'swift': '.swift',
        'kotlin': '.kt',
        'shell': '.sh',
        'bash': '.sh',
        'sql': '.sql',
        'markdown': '.md',
        'xml': '.xml',
        'yaml': '.yaml',
        'yml': '.yml',
        'dockerfile': '',
        'text': '.txt',
        'jsx': '.jsx',
        'tsx': '.tsx',
        'scss': '.scss',
        'sass': '.sass',
        'less': '.less',
        'graphql': '.graphql',
        'haskell': '.hs',
        'r': '.r',
        'matlab': '.m',
        'perl': '.pl',
        'lua': '.lua',
        'scala': '.scala',
        'powershell': '.ps1',
        'cmake': '.cmake',
        'dart': '.dart',
        'groovy': '.groovy',
        'elixir': '.ex',
        'erlang': '.erl',
        'clojure': '.clj',
        'coffeescript': '.coffee',
        'pascal': '.pas',
        'fortran': '.f90',
        'lisp': '.lisp',
        'scheme': '.scm',
        'assembly': '.asm',
        'objective-c': '.m',
        'vbnet': '.vb',
        'vue': '.vue',
        'fs': '.fs',
        'fsharp': '.fs',
        'cobol': '.cob',
        'ini': '.ini',
        'toml': '.toml',
        'diff': '.diff',
        'makefile': '',
        'nginx': '.conf',
        'apache': '.conf',
        'tcl': '.tcl',
        'verilog': '.v',
        'vhdl': '.vhdl',
        'protobuf': '.proto',
        'terraform': '.tf',
        'cucumber': '.feature',
        'pug': '.pug',
        'jade': '.jade',
        'handlebars': '.hbs',
        'mustache': '.mustache',
        'ejs': '.ejs',
        'django': '.html',
        'twig': '.twig',
        'latex': '.tex',
        'stan': '.stan',
        'solidity': '.sol',
        'ocaml': '.ml',
        'julia': '.jl',
        'crystal': '.cr',
        'nim': '.nim',
        'reason': '.re',
        'elm': '.elm',
        'purescript': '.purs',
        'haxe': '.hx',
        'applescript': '.applescript',
        'awk': '.awk',
        'bat': '.bat',
        'bicep': '.bicep',
        'smithy': '.smithy',
        'puppet': '.pp',
        'apex': '.cls',
        'abap': '.abap',
        'plsql': '.pls',
        'tsql': '.sql',
        'plaintext': '.txt',
        'properties': '.properties',
        'conf': '.conf',
        'log': '.log',
        'csv': '.csv',
        'tsv': '.tsv',
        'env': '.env',
        'gitignore': '.gitignore',
        'gitattributes': '.gitattributes',
        'gradle': '.gradle',
        'svelte': '.svelte'
    };
    
    return languageToExtension[language.toLowerCase()] || '.txt';
}
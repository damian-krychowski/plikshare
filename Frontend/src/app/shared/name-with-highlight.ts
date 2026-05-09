export function getNameWithHighlight(name: string, searchPhraseLower: string): string {
    if (!searchPhraseLower) {
        return escapeHtml(name);
    }

    const nameLowered = name.toLowerCase();
    const startIndex = nameLowered.indexOf(searchPhraseLower);

    if (startIndex < 0) {
        return escapeHtml(name);
    }

    const endIndex = startIndex + searchPhraseLower.length;
    const before = escapeHtml(name.slice(0, startIndex));
    const match = escapeHtml(name.slice(startIndex, endIndex));
    const after = escapeHtml(name.slice(endIndex));

    return `${before}<strong>${match}</strong>${after}`;
}

function escapeHtml(s: string): string {
    return s
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}

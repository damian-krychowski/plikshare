export function getNameWithHighlight(name: string, searchPhraseLower: string): string {
    const nameLowered = name.toLowerCase();

    if (!nameLowered.includes(searchPhraseLower)) {
        return name;
    }

    const startIndex = nameLowered.indexOf(searchPhraseLower);
    const endIndex = startIndex + searchPhraseLower.length;
    const highlighted = `${name.slice(0, startIndex)}<strong>${name.slice(startIndex, endIndex)}</strong>${name.slice(endIndex)}`;
    
    return highlighted;
}
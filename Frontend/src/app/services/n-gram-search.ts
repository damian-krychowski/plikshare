export class NGramSearch<T> {
    private ngramIndex: Map<string, Set<T>> = new Map();
    private bloomFilter: Set<string> = new Set();
    private readonly ngramSize = 3;
    
    constructor(
        entries: T[], 
        private getPhraseLower: (entry: T) => string) {
        entries.forEach(entry => {
            const phraseLower = getPhraseLower(entry);
            
            // Index ngrams and add to bloom filter
            const ngrams = this.generateNGrams(
                phraseLower);

            ngrams.forEach(ngram => {
                this.bloomFilter.add(ngram); // Add ngrams to bloom filter
                if (!this.ngramIndex.has(ngram)) {
                    this.ngramIndex.set(ngram, new Set());
                }
                this.ngramIndex.get(ngram)!.add(entry);
            });
        });
    }

    search(query: string): T[] {
        const queryLower = query.toLowerCase();
        const queryNgrams = this.generateNGrams(queryLower);
        
        // Quick rejection using bloom filter for ngrams
        if (!queryNgrams.some(ngram => this.bloomFilter.has(ngram))) {
            return [];
        }

        const candidates = new Map<T, number>();
        
        queryNgrams.forEach(ngram => {
            const matchingEntries = this.ngramIndex.get(ngram);
            if (matchingEntries) {
                matchingEntries.forEach(entry => {
                    candidates.set(entry, (candidates.get(entry) || 0) + 1);
                });
            }
        });
        
        return Array
            .from(candidates.entries())
            .filter(([_, count]) => count === queryNgrams.length)
            .map(([entry]) => entry)
            .filter(entry => this.getPhraseLower(entry).includes(query));
    }

    private generateNGrams(text: string): string[] {
        const ngrams = new Set<string>();
        for (let i = 0; i <= text.length - this.ngramSize; i++) {
            ngrams.add(text.slice(i, i + this.ngramSize));
        }
        return Array.from(ngrams);
    }
}
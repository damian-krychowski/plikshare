const fs = require('fs-extra');
const path = require('path');
const concat = require('concat');

// Configuration
const args = process.argv.slice(2);
const outputDir = args[0] || '../PlikShare/wwwroot/browser/elements';
const outputFilename = args[1] || 'elements';

async function mergeElements() {
  try {
    // Get absolute path for more reliable file operations
    const absolutePath = path.resolve(outputDir);
    console.log(`Looking for JS files in: ${absolutePath}`);
    
    // Check if directory exists
    if (!fs.existsSync(absolutePath)) {
      console.error(`Error: Directory does not exist: ${absolutePath}`);
      console.log('Current working directory:', process.cwd());
      process.exit(1);
    }
    
    // Read the directory directly, no glob patterns
    const allFiles = fs.readdirSync(absolutePath);
    console.log('All files in directory:');
    allFiles.forEach(file => console.log(`- ${file}`));
    
    // Filter for JavaScript files
    const jsFiles = allFiles
      .filter(file => file.endsWith('.js'))
      .map(file => path.join(absolutePath, file));
      
    console.log('Found JS files:', jsFiles);
    
    if (jsFiles.length === 0) {
      console.error('No JavaScript files found in the output directory');
      process.exit(1);
    }
    
    // Sort files to ensure a consistent order (runtime, polyfills, main, scripts)
    const sortOrder = ['runtime', 'polyfills', 'main', 'scripts'];
    jsFiles.sort((a, b) => {
      const fileNameA = path.basename(a);
      const fileNameB = path.basename(b);
      
      for (const prefix of sortOrder) {
        const aStartsWithPrefix = fileNameA.startsWith(prefix);
        const bStartsWithPrefix = fileNameB.startsWith(prefix);
        
        if (aStartsWithPrefix && !bStartsWithPrefix) return -1;
        if (!aStartsWithPrefix && bStartsWithPrefix) return 1;
        if (aStartsWithPrefix && bStartsWithPrefix) {
          return 0; // Both start with the same prefix, maintain order
        }
      }
      
      return fileNameA.localeCompare(fileNameB);
    });
    
    console.log('Files to merge (in order):', jsFiles);
         
    // Create the output filename
    const outputFile = path.join(absolutePath, `${outputFilename}.js`);
    
    // Merge the files
    await concat(jsFiles, outputFile);
    
    console.log(`Successfully merged files into: ${outputFile}`);
    
    // Delete source files
    console.log('Deleting source files...');
    for (const file of jsFiles) {
      try {
        fs.removeSync(file);
        console.log(`Deleted: ${path.basename(file)}`);
      } catch (err) {
        console.error(`Failed to delete ${path.basename(file)}: ${err.message}`);
      }
    }
    console.log('Source files removed');
    
  } catch (error) {
    console.error('Error merging files:', error);
    process.exit(1);
  }
}

mergeElements();
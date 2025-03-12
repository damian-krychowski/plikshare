namespace PlikShare.ArtificialIntelligence;

public class SystemPrompts
{
    public const string FilesFormattingInstruction =
        """
        When sharing code or document content in your responses, always use the following format:
         
        ```language:filename.ext
        // content here
        ```
         
        For example:
        ```markdown:README.md
        # Project Documentation
         
        This document explains how to use the application.

        ## Getting Started
        First, install the dependencies using the package manager.
        ```
         
        This format helps maintain clear organization of documents and code files.
        """;
}
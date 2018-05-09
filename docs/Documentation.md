# Getting started

## Definitions

### Provider
The provider is the entity that generated the specific XML document being processed.

### Repository
The repository is the database the shredded XML data will be stored in. It can either be created manually before the first run, or by the application's first run. The repository may also need to be upgraded from an older version, and the app will also do that as needed, or can generate a script to do that.
For more info on how to get a database creation or upgrade script, see the [console help page](../ConsoleRunner/Resources/manpage.txt).

### Source
The source is the file system or SQL Server database the raw XML is stored in. 
 - For database sources, you'll need to specify a source _connection_ and source _object_. The source _object_ can be a table, a view, or a query (or the name of a file that contains one of those). If the source object is a query, it cannot contain a common-table expression. You can also specify a source _timeout_.
 - For file system sources, you'll need to specify a root folder. You can also specify a filter to only pull certain files.

The table, view, or query must return these columns:
* **DocumentID** - The unique ID for the XML document (a non-null value that will be converted to a string).
* **ProviderName** - Must be a non-null string.
* **GenerationDate** - The date the document was created. Can be null.
* **XML** - the full text of the XML document.
* **SubjectID** - the ID of the entity that this XML document concerns (e.g., the Customer ID)

### Model

XML to Table supports two different kinds of models, hierarchical and key-value.

#### Hierarchical
When building a hierarchical model, XML to Table will create tables to correspond to the XML elements being processed. It can relate these tables with foreign keys, or through a set of ParentID and ParentTable columns. The names of tables and columns can be enforced to be a certain length.

#### Key-Value
When building a key-value model, XML to Table will insert unique XPaths into a "Variables" table, and values into a "DocumentVariables" table that links back to the DocumentID and the VariableID.

# Configuration

XML to Table can be run by specifying all of the parameters in a config file, or by specifying all the parameters on the command line. A config file setup allows you to run XML to Table over and over again without having to create a cumbersome batch script, while a command-line run allows you to quickly shred some XML without a lengthy setup.

If your configuration file contains passwords or other sensitive data, be sure to [protect](https://msdn.microsoft.com/en-us/library/hh8x3tas(v=vs.100).aspx) the file before deploying it. A handy [script](../protect_config.bat) to do this is provided in the source (it takes the full path of the config file and the section to encrypt as arguments).

For detail on how to set up a config file, please view the [Example.config](../ConsoleRunner/Example.config).
For details on how to set up the command-line, please view the [console help page](../ConsoleRunner/Resources/manpage.txt).

## For further info, please see [Questions you might ask](Questions-you-might-ask.md).

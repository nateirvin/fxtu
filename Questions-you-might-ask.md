## **Does the repository have to be created first?**

No. XML to Table will create the database if it does not exist.

If the application user does not have rights to create databases, or you just want to create the database first, the application can make a database creation script by using this command-line option:

**-g=<file>**

You can specify other options as needed - if run with just option, the script will use only default values. For more information on the command line, please read the [console help page](https://fxtu.codeplex.com/SourceControl/latest#master/ConsoleRunner/Resources/manpage.txt).


## **What permissions are needed?**

On the source, the app only needs permission to SELECT from the table(s) and/or view(s) specified by the source object or query.

In the repository database, the app will need permission to:
- SELECT FROM sys.databases & sys.columns
- EXECUTE all stored procedures
- VIEW DEFINITION on all tables
- CREATE and ALTER tables (hierarchical model only)
- INSERT on all tables (hierarchical model only)
If the app will be creating/upgrading the database as well, it will also need these permission in the repository database:
- CREATE DATABASE
- CREATE, ALTER, DROP, and EXECUTE on all stored procedures
- CREATE and DROP types
- UPDATE on all tables

It is recommended that the user executing database upgrades not have any rights outside the repository database to prevent SQL injection attacks.


## **Are there 32-bit and/or 64-bit versions of this software?**

XML to Table targets both 32-bit and 64-bit Windows.


## **How fast can it go?**

Speed depends on a lot of factors. The XML parsing is fairly CPU intensive, so the more cores you can throw at it, the better. If you are parsing large documents, the database write step for each batch can be a serious bottleneck, so it's recommended that you run the app on the same machine as the database itself. If you have large documents, smaller batch sizes (500-1000 documents) are recommended, as it reduces the chance of the batch failing and having to be redone. If you have relatively small documents, larger batch sizes can help throughput. 

As a more concrete baseline, on Windows Server 2012 machine with 4 GB of RAM, and a quad-core 2.60 GHz Intel Xeon processor that also hosted the database, XML to Table was able to shred 2 million documents in only a few hours.

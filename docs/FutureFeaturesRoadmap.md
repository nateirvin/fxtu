In order of priority, here are the features we want to implement in the future:

1. Thread-safety & multiple instances
1. Encrypting sensitive data
1. Importing from databases other than SQL Server
1. Localization
1. Feature and/or guidelines to estimate the size of the database based on the XML dataset

At this time there are no plans to target other database platforms (for the repository), for two main reasons:

* XML to Table relies on many features unique to SQL Server (e.g., table-valued parameters, SQL Bulk Copy). 
* There doesn't seem to be much call for it. MySQL apparently already has something similar, and single-container databases like SQL Compact and SQLite seem like poor candidates for the high-volume data re-normalizing XML to Table is built for. And of course NoSQL databases are a totally different ballgame.

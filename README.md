# ![Icon](https://github.com/lithnet/miis-autosync/wiki/images/change-sync.png) Lithnet AutoSync for Microsoft Identity Manager

The Lithnet FIM/MIM AutoSync Service is a windows service that runs along side of the FIM/MIM Synchronization service, and automatically executes management agent run profiles.

#### What it does 
AutoSync has a unique way of managing the synchronization engine's run profiles which differs from other tools where you define an execution order for the various import, export, and synchronization profiles. AutoSync automatically performs exports and delta synchronizations when changes are detected in the sync engine, and only import operations need to be 'defined'.

#### How it works
Whenever an import is performed, AutoSync will check for staged changes in the MA, if there are changes staged, it will automatically perform a delta synchronization on that management agent. If the synchronization results in outbound changes (exports), an export job is kicked off on each management agent. A confirming import will be performed after the export, and once again, if there are changes staged from the import, a delta synchronization will be performed. AutoSync will continue the cycle until all changes have been processed. 

### Triggering imports
AutoSync provides several out-of-box solutions for triggering import operations. Time-based triggers allow you to trigger operations at regular intervals or at a scheduled day and time. Some management agents contain built-in triggers for change detection (MIM service and Active Directory).

The most powerful mechanism provided is the PowerShell trigger. Administrators can use PowerShell to write their own custom triggers that may, for example, detect if there are pending changes in a system, and trigger an import operation.

See the wiki topic on triggers for more information

#### Synchronization profiles
The FIM/MIM Synchronziation engine allows only a single synchronization run profile to be executed at any one time. AutoSync ensures that only one synchronization can run at a time by queing requests and executing them in sequence.

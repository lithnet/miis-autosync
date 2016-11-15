# ![Icon](https://github.com/lithnet/miis-autosync/wiki/images/change-sync.png) Lithnet AutoSync for Microsoft Identity Manager

The Lithnet FIM/MIM AutoSync Service is a windows service that runs along side of the FIM/MIM Synchronization service, and automatically executes management agent run profiles.

#### What it does 
AutoSync has a unique way of managing the synchronization engine's run profiles which differs from other tools where you define an execution order for the various import, export, and synchronization profiles. AutoSync automatically performs exports and delta synchronizations when changes are detected in the sync engine, and only import operations need to be 'defined'.

Out of the box, AutoSync will setup import profiles based on prior run history, management agent types, and provides the option for providing manual triggers using PowerShell.

It also has the ability to provide email notifications when a run profile fails.

#### How it works
Whenever an import is performed, AutoSync will check for staged changes in the MA, if there are changes staged, it will automatically perform a delta synchronization on that management agent. If the synchronization results in outbound changes (exports), an export job is kicked off on each management agent. A confirming import will be performed after the export, and once again, if there are changes staged from the import, a delta synchronization will be performed. AutoSync will continue the cycle until all changes have been processed. 

#### Automatic configuration
When the service starts, it will automatically detect each management agent's run profiles and perform the following actions

1. Determine which management agents are considered 'source' vs 'target' MAs (A management agent with more import attribute flows than export attribute flows is considered as source MA)
2. Examine the run history for the 'source' management agents to determine how frequently imports have been run, and sets up an import schedule
3. If a FIM MA exists, connects to the FIM service and polls for changes every 60 seconds
4. If an AD MA exists, connects to the AD and subscribes to change notifications

#### Manual Configuration
##### Triggers
AutoSync extends control to administrators using PowerShell scripts. Each MA can have any number of 'trigger' scripts that can be used to have AutoSync execute run profiles. Triggers can be used to check for changes in a system, or perform scheduled operations, such as a nightly full import, or a weekly full synchronization.

See the wiki topic on triggers for more information

##### Overriding defaults
While AutoSync configures itself out-of-the-box, and the discovered configuration will work for most scenarios, administrators can take control of the behaviour using PowerShell scripts. Each MA can be fully configured manually if the discovered settings are not correct.

#### Synchronization profiles
The FIM/MIM Synchronziation engine allows only a single synchronization run profile to be executed at any one time. AutoSync ensures that only one synchronization can run at a time by queing requests and executing them in sequence.

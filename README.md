# ![Icon](https://github.com/lithnet/miis-autosync/wiki/images/autosync-logo-sm.png)

### Product Overview
AutoSync is a windows service that runs along side of the FIM/MIM synchronization service, and automatically executes management agent run profiles. AutoSync automatically performs exports and delta synchronizations when changes are detected in the sync engine. AutoSync relies on you to define when import operations should run, and provides various event-based and scheduled mechanisms for you to do so.

#### How it works
Whenever an import is performed, AutoSync will check for staged changes in the MA, if there are changes staged, it will automatically perform a delta synchronization on that management agent. If the synchronization results in outbound changes (exports), an export job is kicked off on each management agent. A confirming import will be performed after the export, and once again, if there are changes staged from the import, a delta synchronization will be performed. AutoSync will continue the cycle until all changes have been processed.

#### Triggering imports
AutoSync provides several out-of-box solutions for triggering import operations. Time-based triggers allow you to trigger operations at regular intervals or at a scheduled day and time. Some management agents contain built-in triggers for change detection (MIM service and Active Directory).

The most powerful mechanism provided is the PowerShell trigger. Administrators can use PowerShell to write their own custom triggers that may, for example, detect if there are pending changes in a system, and trigger an import operation.

See the wiki topic on [triggers](https://github.com/lithnet/miis-autosync/wiki/Triggers) for more information

#### Synchronization profiles
AutoSync follows the Microsoft guidelines when it comes to running multiple management agents simultaneously. While import and export operations are allowed to overlap, synchronizations must be run exclusively. See the [advanced settings](https://github.com/lithnet/miis-autosync/wiki/Advanced-settings) topic for more information.

### Guides
*   [Prequisites](https://github.com/lithnet/miis-autosync/wiki/Prerequisites)
*   [Installing AutoSync](https://github.com/lithnet/miis-autosync/wiki/Installing-AutoSync)
*   [Triggers](https://github.com/lithnet/miis-autosync/wiki/Triggers)

### Download the software
Download the [current release](https://github.com/lithnet/miis-autosync/releases/)

## How can I contribute to the project?
* Found an issue and want us to fix it? [Log it](https://github.com/lithnet/miis-autosync/issues)
* Want to fix an issue yourself or add functionality? Clone the project and submit a pull request

## Enterprise support
Lithnet offer enterprise support plans for our open-source products. Deploy our tools with confidence that you have the backing of the dedicated Lithnet support team if you run into any issues, have questions, or need advice. Simply fill out the [request form](https://lithnet.io/products/mim), let us know the number of users you are managing with your MIM implementation, and we'll put together a quote.

### Keep up to date
*   [Visit our blog](http://blog.lithnet.io)
*   [Follow us on twitter](https://twitter.com/lithnet_io)![](http://twitter.com/favicon.ico)

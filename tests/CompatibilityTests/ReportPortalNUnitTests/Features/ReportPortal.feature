Feature: Ohirok plugins together with ReportPortal.Reqnroll

Scenario: The binding invoker chain wraps ReportPortal's invoker
	Then the binding invoker chain ends with SafeBindingInvoker under the Ohirok links

@retry:2
Scenario: Retry works next to ReportPortal, and its hooks can log to it
	Given a step that fails on its first attempt
	Then the OnRetry hook has run

Scenario: XFail works next to ReportPortal, and its hooks can log to it
	# #xfail:InvalidOperationException
	Given a step that throws InvalidOperationException
	Then the OnXFail hook has run

Feature: A plugin on an older SDK next to the Ohirok plugins

  # Catchy.ReqnrollPlugin 0.0.5 is built on SDK 0.0.0 and is loaded before the other generator plugins here.
  # The scenario still becomes one test per browser only if the generator uses the newest SDK.

  @Browser:Chrome @Browser:Firefox
  Scenario: Variants work with an older SDK plugin loaded first
    Then the browser is one of the variants

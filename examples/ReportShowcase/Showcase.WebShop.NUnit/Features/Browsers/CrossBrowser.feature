@ui
Feature: The shop in every browser
  The storefront is checked in each supported browser. A scenario tagged with the browsers becomes one test per
  browser (Ohirok.Variants, key "Browser" in reqnroll.json); the value is <Browser> in the steps and in the title.

  @Browser:Chrome @Browser:Firefox @Browser:Safari
  Scenario: Search works in <Browser>
    Given the storefront is opened in <Browser>
    Then the page renders in <Browser>

  @Browser:Chrome @Browser:Firefox
  Scenario Outline: The cart badge shows <items> item(s) in <Browser>
    Given the storefront is opened in <Browser>
    Then the cart badge shows <items> in <Browser>

    Examples:
      | items |
      | 1     |
      | 3     |

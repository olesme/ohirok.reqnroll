# Every Gherkin construct in one file, to see how the report renders each of them: tags at every level,
# free text under every header, a named Background at feature and at rule level, a Rule, a Scenario Outline with
# several tagged Examples blocks, a data table, a doc string, comments and the And/But keywords.
@gherkin @p2
Feature: Every Gherkin construct
  This description sits under the Feature header.
  It can span several lines.

  Background: A member shopping from Germany
    Free text under a Background header.

    Given a member customer
    And the customer ships to DE

  Scenario: A cart from a table and a note from a doc string
    Given the cart contains:
      | sku     | quantity |
      | TEA-001 | 1        |
      | ACC-021 | 2        |
    And the order note is:
      """json
      { "deliverAfter": "18:00", "leaveWithNeighbour": false }
      """
    Then the subtotal is 25.50 EUR
    And the order note is kept verbatim

  @tableware
  Rule: Tableware orders
    Free text under a Rule header.

    Background: The cart already holds a cup
      Given the cart contains 1 x CUP-010

    Scenario: Adding a teapot
      Free text under a Scenario header.

      When the customer adds 1 x CUP-011
      Then the subtotal is 60.00 EUR
      # A comment between steps
      And the order weighs 1.50 kg
      But the discount is 6.00 EUR

    @outline
    Scenario Outline: Adding <count> teapot(s)
      Free text under a Scenario Outline header.

      When the customer adds <count> x CUP-011
      Then the subtotal is <subtotal> EUR

      @small
      Examples: Small orders
        Free text under an Examples header.

        | count | subtotal |
        | 1     | 60.00    |
        | 2     | 102.00   |

      @large
      Examples: Large orders
        | count | subtotal |
        | 10    | 438.00   |

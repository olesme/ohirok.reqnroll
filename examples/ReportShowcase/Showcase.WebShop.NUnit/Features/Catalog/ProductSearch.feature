@catalog @p2
Feature: Product search
  Customers find products by name or category. Searching is case-insensitive.

  Scenario: Searching by category finds every product in it
    When the customer searches for "tea"
    Then 6 products are found
    And the results contain:
      | sku     |
      | TEA-001 |
      | TEA-002 |
      | TEA-003 |
      | CUP-011 |
      | ACC-021 |
      | GFT-030 |

  Scenario Outline: Searching by name - <text>
    When the customer searches for "<text>"
    Then <count> products are found

    Examples: Exact names
      | text          | count |
      | Earl Grey     | 1     |
      | Bamboo whisk  | 1     |

    Examples: Partial and mixed case
      | text    | count |
      | KETTLE  | 1     |
      | gift    | 2     |
      | cup     | 1     |

  Scenario: Searching for something the shop does not sell finds nothing
    When the customer searches for "coffee"
    Then 0 products are found

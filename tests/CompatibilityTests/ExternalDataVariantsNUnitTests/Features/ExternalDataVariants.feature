Feature: Reqnroll.ExternalData together with Variants

@DataSource:products.csv @Browser:Chrome @Browser:Firefox
Scenario: Every product in every browser
	Given the product <product> in <Browser>

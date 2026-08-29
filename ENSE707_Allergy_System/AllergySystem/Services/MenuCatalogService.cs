using AllergySystem.Models;

namespace AllergySystem.Services
{
    public class MenuCatalogService
    {
        private readonly AllergenCatalogService _allergenCatalogService;

        public MenuCatalogService(AllergenCatalogService allergenCatalogService)
        {
            _allergenCatalogService = allergenCatalogService;
        }

        public List<MenuItem> GetMenuItems()
        {
            var allergens = _allergenCatalogService.GetAllergens().ToDictionary(a => a.Id);

            return new List<MenuItem>
            {
                new MenuItem
                {
                    Id = 1,
                    Name = "Classic Cheeseburger",
                    Description = "Beef burger with cheese, lettuce and tomato on a sesame bun.",
                    Ingredients = new List<Ingredient>
                    {
                        new Ingredient
                        {
                            Id = 1,
                            Name = "Beef Patty"
                        },
                        new Ingredient
                        {
                            Id = 2,
                            Name = "Cheese",
                            Allergens = new List<Allergen>
                            {
                                allergens[3]
                            }
                        },
                        new Ingredient
                        {
                            Id = 3,
                            Name = "Burger Bun",
                            Allergens = new List<Allergen>
                            {
                                allergens[5],
                                allergens[9]
                            }
                        },
                        new Ingredient
                        {
                            Id = 4,
                            Name = "Lettuce"
                        },
                        new Ingredient
                        {
                            Id = 5,
                            Name = "Tomato"
                        }
                    }
                },

                new MenuItem
                {
                    Id = 2,
                    Name = "Peanut Chicken Noodles",
                    Description = "Chicken and noodles served with a peanut and soy sauce.",
                    Ingredients = new List<Ingredient>
                    {
                        new Ingredient
                        {
                            Id = 6,
                            Name = "Chicken"
                        },
                        new Ingredient
                        {
                            Id = 7,
                            Name = "Noodles",
                            Allergens = new List<Allergen>
                            {
                                allergens[4],
                                allergens[5]
                            }
                        },
                        new Ingredient
                        {
                            Id = 8,
                            Name = "Peanut Sauce",
                            Allergens = new List<Allergen>
                            {
                                allergens[1],
                                allergens[6]
                            }
                        }
                    }
                },

                new MenuItem
                {
                    Id = 3,
                    Name = "Fish and Chips",
                    Description = "Battered fish served with chips.",
                    Ingredients = new List<Ingredient>
                    {
                        new Ingredient
                        {
                            Id = 9,
                            Name = "Battered Fish",
                            Allergens = new List<Allergen>
                            {
                                allergens[7],
                                allergens[5]
                            }
                        },
                        new Ingredient
                        {
                            Id = 10,
                            Name = "Chips"
                        }
                    }
                },

                new MenuItem
                {
                    Id = 4,
                    Name = "Creamy Pasta",
                    Description = "Pasta served in a creamy cheese sauce.",
                    Ingredients = new List<Ingredient>
                    {
                        new Ingredient
                        {
                            Id = 11,
                            Name = "Pasta",
                            Allergens = new List<Allergen>
                            {
                                allergens[4],
                                allergens[5]
                            }
                        },
                        new Ingredient
                        {
                            Id = 12,
                            Name = "Cream Sauce",
                            Allergens = new List<Allergen>
                            {
                                allergens[3]
                            }
                        }
                    }
                },

                new MenuItem
                {
                    Id = 5,
                    Name = "Garden Salad",
                    Description = "Fresh lettuce, tomato and cucumber with olive oil.",
                    Ingredients = new List<Ingredient>
                    {
                        new Ingredient
                        {
                            Id = 13,
                            Name = "Lettuce"
                        },
                        new Ingredient
                        {
                            Id = 14,
                            Name = "Tomato"
                        },
                        new Ingredient
                        {
                            Id = 15,
                            Name = "Cucumber"
                        },
                        new Ingredient
                        {
                            Id = 16,
                            Name = "Olive Oil"
                        }
                    }
                }
            };
        }
    }
}
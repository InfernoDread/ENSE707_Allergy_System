namespace AllergySystem.Services
{
    public class MenuAllergenMappingStore
    {
        private readonly Dictionary<int, List<int>> _allergenIdsByIngredientId = new();

        public bool TryGetAllergenIds(int ingredientId, out IReadOnlyList<int> allergenIds)
        {
            if (_allergenIdsByIngredientId.TryGetValue(ingredientId, out var storedIds))
            {
                allergenIds = storedIds.ToList();
                return true;
            }

            allergenIds = Array.Empty<int>();
            return false;
        }

        public void SetAllergenIds(int ingredientId, IEnumerable<int> allergenIds)
        {
            ArgumentNullException.ThrowIfNull(allergenIds);
            _allergenIdsByIngredientId[ingredientId] = allergenIds.Distinct().ToList();
        }
    }
}
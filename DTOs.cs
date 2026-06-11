using System.ComponentModel.DataAnnotations;

namespace JasylEl.DTOs
{
    // ===================== AUTH DTOs =====================

    public class RegisterDto
    {
        [Required, MinLength(3), MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, MinLength(6)]
        public string Password { get; set; } = string.Empty;

        [Required]
        public int RegionId { get; set; }
    }

    public class LoginDto
    {
        [Required]
        public string EmailOrUsername { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class AuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public UserProfileDto User { get; set; } = new();
    }

    // ===================== USER DTOs =====================

    public class UserProfileDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int Level { get; set; }
        public int Experience { get; set; }
        public int ExperienceToNextLevel { get; set; }
        public int Coins { get; set; }
        public int Water { get; set; }
        public int Energy { get; set; }
        public int EcoPoints { get; set; }
        public int JasylElReputation { get; set; }
        public RegionDto? Region { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastLoginAt { get; set; }
    }

    public class LeaderboardEntryDto
    {
        public int Rank { get; set; }
        public string Username { get; set; } = string.Empty;
        public int Level { get; set; }
        public int EcoPoints { get; set; }
        public int TreesPlanted { get; set; }
        public string RegionName { get; set; } = string.Empty;
    }

    // ===================== REGION DTOs =====================

    public class RegionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public float EnergyBonus { get; set; }
        public float WaterBonus { get; set; }
        public float GrowthSpeedBonus { get; set; }
        public float CoinsBonus { get; set; }
        public float ExperienceBonus { get; set; }
        public float DroughtRisk { get; set; }
        public float StabilityBonus { get; set; }
        public string MapTexture { get; set; } = string.Empty;
        public string PrimaryColor { get; set; } = string.Empty;
    }

    // ===================== MAP DTOs =====================

    public class MapDataDto
    {
        public List<MapObjectDto> Objects { get; set; } = new();
        public List<EcoEventDto> Events { get; set; } = new();
        public List<NPCDto> NPCs { get; set; } = new();
        public List<MapZoneDto> Zones { get; set; } = new();
    }

    public class MapObjectDto
    {
        public int Id { get; set; }
        public string ObjectType { get; set; } = string.Empty;
        public int? TreeTypeId { get; set; }
        public string? TreeName { get; set; }
        public string? TreeRarity { get; set; }
        public int? BuildingTypeId { get; set; }
        public string? BuildingName { get; set; }
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public string GrowthStage { get; set; } = string.Empty;
        public int BuildingLevel { get; set; }
        public float Health { get; set; }
        public bool IsOnFire { get; set; }
        public bool IsDiseased { get; set; }
        public bool IsDry { get; set; }
        public DateTime PlantedAt { get; set; }
        public DateTime NextGrowthAt { get; set; }
        public string? Sprite { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public string TrunkColor { get; set; } = "#8B4513";
        public string LeafColor { get; set; } = "#228B22";
    }

    public class PlantTreeDto
    {
        [Required]
        public int TreeTypeId { get; set; }

        [Required]
        public float PositionX { get; set; }

        [Required]
        public float PositionY { get; set; }
    }

    public class PlaceBuildingDto
    {
        [Required]
        public int BuildingTypeId { get; set; }

        [Required]
        public float PositionX { get; set; }

        [Required]
        public float PositionY { get; set; }
    }

    public class CanPlantResultDto
    {
        public bool CanPlant { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string ZoneType { get; set; } = "Neutral";
        public float GrowthMultiplier { get; set; } = 1f;
        public float WaterCostMultiplier { get; set; } = 1f;
    }

    public class UpgradeBuildingDto
    {
        [Required]
        public int MapObjectId { get; set; }
    }

    public class ResolveEventDto
    {
        [Required]
        public int EventId { get; set; }
    }

    // ===================== QUIZ DTOs =====================

    public class QuizCategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public int TotalQuestions { get; set; }
        public int CompletedByUser { get; set; }
    }

    public class QuizQuestionDto
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string QuestionText { get; set; } = string.Empty;
        public int Difficulty { get; set; }
        public List<string> AnswerOptions { get; set; } = new();
        public int RewardCoins { get; set; }
        public int RewardExperience { get; set; }
        public int RewardWater { get; set; }
        public int RewardEnergy { get; set; }
    }

    public class SubmitAnswerDto
    {
        [Required]
        public int QuestionId { get; set; }

        [Required, Range(0, 3)]
        public int AnswerIndex { get; set; }
    }

    public class QuizResultDto
    {
        public bool IsCorrect { get; set; }
        public string Explanation { get; set; } = string.Empty;
        public int RewardedCoins { get; set; }
        public int RewardedExperience { get; set; }
        public int RewardedWater { get; set; }
        public int RewardedEnergy { get; set; }
        public string? UnlockedTree { get; set; }
        public int NewLevel { get; set; }
        public int NewExperience { get; set; }
        public List<string> NewAchievements { get; set; } = new();
    }

    // ===================== ACHIEVEMENT DTOs =====================

    public class AchievementDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string AchievementType { get; set; } = string.Empty;
        public int TargetValue { get; set; }
        public int CurrentProgress { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int RewardCoins { get; set; }
        public int RewardExperience { get; set; }
    }

    // ===================== TREE DTOs =====================

    public class TreeTypeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Rarity { get; set; } = string.Empty;
        public int CostCoins { get; set; }
        public int CostWater { get; set; }
        public int CostEnergy { get; set; }
        public int UnlockLevel { get; set; }
        public bool IsUnlocked { get; set; }
        public int EcoPointsProduction { get; set; }
        public int WaterProduction { get; set; }
        public float BaseWidth { get; set; }
        public float BaseHeight { get; set; }
        public string TrunkColor { get; set; } = string.Empty;
        public string LeafColor { get; set; } = string.Empty;
    }

    // ===================== EVENT DTOs =====================

    public class EcoEventDto
    {
        public int Id { get; set; }
        public string EventType { get; set; } = string.Empty;
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public bool IsResolved { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? MapObjectId { get; set; }
    }

    public class MapZoneDto
    {
        public int Id { get; set; }
        public string ZoneType { get; set; } = "Neutral";
        public string Name { get; set; } = string.Empty;
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public bool CanPlant { get; set; } = true;
        public float GrowthMultiplier { get; set; } = 1f;
        public float WaterCostMultiplier { get; set; } = 1f;
        public float DroughtRiskModifier { get; set; }
        public string Visual { get; set; } = "neutral";
    }

    // ===================== NPC DTOs =====================

    public class NPCDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Sprite { get; set; } = string.Empty;
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public float TargetX { get; set; }
        public float TargetY { get; set; }
    }

    public class HireNPCDto
    {
        [Required]
        public int NPCTypeId { get; set; }
    }

    // ===================== BUILDING DTOs =====================

    public class BuildingTypeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Rarity { get; set; } = string.Empty;
        public int CostCoins { get; set; }
        public int CostEnergy { get; set; }
        public int MaxLevel { get; set; }
        public int UnlockLevel { get; set; }
        public bool IsUnlocked { get; set; }
        public float BaseWidth { get; set; }
        public float BaseHeight { get; set; }
    }
}

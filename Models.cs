using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JasylEl.Models
{
    // ===================== ПОЛЬЗОВАТЕЛИ =====================

    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        public int Level { get; set; } = 1;
        public int Experience { get; set; } = 0;
        public int ExperienceToNextLevel { get; set; } = 100;

        // Ресурсы
        public int Coins { get; set; } = 500;
        public int Water { get; set; } = 200;
        public int Energy { get; set; } = 100;
        public int EcoPoints { get; set; } = 0;
        public int JasylElReputation { get; set; } = 0;

        public int RegionId { get; set; }
        public Region? Region { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;

        // Навигационные свойства
        public ICollection<UserAchievement> UserAchievements { get; set; } = new List<UserAchievement>();
        public ICollection<MapObject> MapObjects { get; set; } = new List<MapObject>();
        public ICollection<QuizResult> QuizResults { get; set; } = new List<QuizResult>();
        public ICollection<UserNPC> UserNPCs { get; set; } = new List<UserNPC>();
    }

    // ===================== РЕГИОНЫ =====================

    public class Region
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        // ИСПРАВЛЕНИЕ: Добавлено свойство EcoPoints
        public int EcoPoints { get; set; } = 0;

        // Бонусы региона
        public float EnergyBonus { get; set; } = 0;
        public float WaterBonus { get; set; } = 0;
        public float GrowthSpeedBonus { get; set; } = 0;
        public float CoinsBonus { get; set; } = 0;
        public float ExperienceBonus { get; set; } = 0;
        public float DroughtRisk { get; set; } = 0.1f;
        public float StabilityBonus { get; set; } = 0;

        // Визуальные настройки карты
        public string MapTexture { get; set; } = "default";
        public string PrimaryColor { get; set; } = "#4CAF50";

        public ICollection<User> Users { get; set; } = new List<User>();
    }

    // ===================== ДЕРЕВЬЯ =====================

    public class TreeType
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string Rarity { get; set; } = "Common";

        // Стоимость посадки
        public int CostCoins { get; set; } = 50;
        public int CostWater { get; set; } = 20;
        public int CostEnergy { get; set; } = 10;

        // Бонусы от взрослого дерева
        public int WaterProduction { get; set; } = 1;
        public int EcoPointsProduction { get; set; } = 2;
        public int OxygenBonus { get; set; } = 1;

        // Условия разблокировки
        public int UnlockLevel { get; set; } = 1;
        public string? UnlockCondition { get; set; }

        // Спрайты для каждой стадии роста
        public string SpriteSeed { get; set; } = "seed";
        public string SpriteSapling { get; set; } = "sapling";
        public string SpriteSprout { get; set; } = "sprout";
        public string SpriteAdult { get; set; } = "adult";

        // Визуальные параметры
        public float BaseWidth { get; set; } = 64;
        public float BaseHeight { get; set; } = 64;
        public string TrunkColor { get; set; } = "#8B4513";
        public string LeafColor { get; set; } = "#228B22";

        // Время роста в минутах для каждой стадии
        public int TimeToSapling { get; set; } = 5;
        public int TimeToSprout { get; set; } = 15;
        public int TimeToAdult { get; set; } = 30;

        public bool IsUnlockedByDefault { get; set; } = false;
    }

    // ===================== ПОСТРОЙКИ =====================

    public class BuildingType
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string Rarity { get; set; } = "Common";

        // Стоимость строительства
        public int CostCoins { get; set; } = 100;
        public int CostWater { get; set; } = 0;
        public int CostEnergy { get; set; } = 50;

        // Бонусы постройки
        public int WaterStorageBonus { get; set; } = 0;
        public int ResourceStorageBonus { get; set; } = 0;
        public int EcoBonus { get; set; } = 0;
        public int ResearchBonus { get; set; } = 0;

        public int MaxLevel { get; set; } = 5;
        public int UnlockLevel { get; set; } = 1;

        // Спрайты по уровням
        public string SpriteLevel1 { get; set; } = "building_l1";
        public string SpriteLevel2 { get; set; } = "building_l2";
        public string SpriteLevel3 { get; set; } = "building_l3";

        public float BaseWidth { get; set; } = 96;
        public float BaseHeight { get; set; } = 96;
    }

    // ===================== ОБЪЕКТЫ НА КАРТЕ =====================

    public class MapObject
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        [Required, MaxLength(20)]
        public string ObjectType { get; set; } = "Tree";

        // Для деревьев
        public int? TreeTypeId { get; set; }
        public TreeType? TreeType { get; set; }

        // Для построек
        public int? BuildingTypeId { get; set; }
        public BuildingType? BuildingType { get; set; }

        // Позиция на карте
        public float PositionX { get; set; }
        public float PositionY { get; set; }

        [MaxLength(20)]
        public string GrowthStage { get; set; } = "Seed";

        public int BuildingLevel { get; set; } = 1;
        public float Health { get; set; } = 100f;
        public bool IsOnFire { get; set; } = false;
        public bool IsDiseased { get; set; } = false;
        public bool IsDry { get; set; } = false;

        public DateTime PlantedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastWateredAt { get; set; }
        public DateTime NextGrowthAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;
    }

    // ===================== ДОСТИЖЕНИЯ =====================

    public class Achievement
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Icon { get; set; } = "trophy";

        [MaxLength(50)]
        public string AchievementType { get; set; } = "TreesPlanted";

        public int TargetValue { get; set; } = 1;

        // Награды
        public int RewardCoins { get; set; } = 0;
        public int RewardExperience { get; set; } = 0;
        public int RewardEcoPoints { get; set; } = 0;
        public int? RewardTreeTypeId { get; set; }

        public ICollection<UserAchievement> UserAchievements { get; set; } = new List<UserAchievement>();
    }

    public class UserAchievement
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        public int AchievementId { get; set; }
        public Achievement? Achievement { get; set; }

        public int CurrentProgress { get; set; } = 0;
        public bool IsCompleted { get; set; } = false;
        public DateTime? CompletedAt { get; set; }
    }

    // ===================== ВИКТОРИНЫ =====================

    public class QuizCategory
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(300)]
        public string Description { get; set; } = string.Empty;

        public string Icon { get; set; } = "quiz";

        public ICollection<QuizQuestion> Questions { get; set; } = new List<QuizQuestion>();
    }

    public class QuizQuestion
    {
        [Key]
        public int Id { get; set; }

        public int CategoryId { get; set; }
        public QuizCategory? Category { get; set; }

        [Required, MaxLength(500)]
        public string QuestionText { get; set; } = string.Empty;

        public int Difficulty { get; set; } = 1;

        public string AnswerOptions { get; set; } = "[]";

        public int CorrectAnswerIndex { get; set; } = 0;

        [MaxLength(500)]
        public string Explanation { get; set; } = string.Empty;

        // Награды за правильный ответ
        public int RewardCoins { get; set; } = 10;
        public int RewardExperience { get; set; } = 20;
        public int RewardWater { get; set; } = 5;
        public int RewardEnergy { get; set; } = 5;
        public int? RewardTreeTypeId { get; set; }
    }

    public class QuizResult
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        public int QuestionId { get; set; }
        public QuizQuestion? Question { get; set; }

        public int AnswerIndex { get; set; }
        public bool IsCorrect { get; set; }

        public DateTime AnsweredAt { get; set; } = DateTime.UtcNow;
    }

    // ===================== СОБЫТИЯ =====================

    public class EcoEvent
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        [Required, MaxLength(20)]
        public string EventType { get; set; } = "Trash";

        public float PositionX { get; set; }
        public float PositionY { get; set; }

        public bool IsResolved { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ResolvedAt { get; set; }

        public int? MapObjectId { get; set; }
        public MapObject? MapObject { get; set; }
    }

    // ===================== NPC =====================

    public class NPCType
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(300)]
        public string Description { get; set; } = string.Empty;

        public string Sprite { get; set; } = "npc_default";

        [MaxLength(20)]
        public string Role { get; set; } = "Volunteer";

        public int CostCoins { get; set; } = 200;
        public int UnlockLevel { get; set; } = 3;
    }

    public class UserNPC
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        public int NPCTypeId { get; set; }
        public NPCType? NPCType { get; set; }

        public float PositionX { get; set; }
        public float PositionY { get; set; }

        public float TargetX { get; set; }
        public float TargetY { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime HiredAt { get; set; } = DateTime.UtcNow;
    }

    // ===================== РАЗБЛОКИРОВАННЫЕ ДЕРЕВЬЯ =====================

    public class UserUnlockedTree
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        public int TreeTypeId { get; set; }
        public TreeType? TreeType { get; set; }

        public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;
    }
}
using JasylEl.Models;
using Microsoft.EntityFrameworkCore;

namespace JasylEl.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<TreeType> TreeTypes => Set<TreeType>();
    public DbSet<BuildingType> BuildingTypes => Set<BuildingType>();
    public DbSet<MapObject> MapObjects => Set<MapObject>();
    public DbSet<Achievement> Achievements => Set<Achievement>();
    public DbSet<UserAchievement> UserAchievements => Set<UserAchievement>();
    public DbSet<QuizCategory> QuizCategories => Set<QuizCategory>();
    public DbSet<QuizQuestion> QuizQuestions => Set<QuizQuestion>();
    public DbSet<QuizResult> QuizResults => Set<QuizResult>();
    public DbSet<EcoEvent> EcoEvents => Set<EcoEvent>();
    public DbSet<NPCType> NPCTypes => Set<NPCType>();
    public DbSet<UserNPC> UserNPCs => Set<UserNPC>();
    public DbSet<UserUnlockedTree> UserUnlockedTrees => Set<UserUnlockedTree>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();
        modelBuilder.Entity<UserUnlockedTree>().HasIndex(t => new { t.UserId, t.TreeTypeId }).IsUnique();

        modelBuilder.Entity<Region>().HasData(
            new Region { Id = 1, Name = "Астана", Description = "Энергия столицы и быстрый рост молодых посадок.", EnergyBonus = 0.2f, GrowthSpeedBonus = 0.15f, MapTexture = "capital", PrimaryColor = "#2f80ed" },
            new Region { Id = 2, Name = "Алматы", Description = "Горный регион с запасами воды и мягким климатом.", GrowthSpeedBonus = 0.25f, WaterBonus = 0.2f, MapTexture = "mountain", PrimaryColor = "#27ae60" },
            new Region { Id = 3, Name = "Шымкент", Description = "Солнечный юг: больше монет, но выше риск засухи.", CoinsBonus = 0.2f, DroughtRisk = 0.35f, MapTexture = "south", PrimaryColor = "#f2c94c" },
            new Region { Id = 4, Name = "Караганда", Description = "Степной центр: больше опыта, меньше воды.", ExperienceBonus = 0.2f, WaterBonus = -0.1f, MapTexture = "steppe", PrimaryColor = "#8d6e63" },
            new Region { Id = 5, Name = "Костанай", Description = "Стабильная территория со сбалансированными условиями.", StabilityBonus = 0.3f, DroughtRisk = 0.1f, MapTexture = "plain", PrimaryColor = "#9ccc65" },
            new Region { Id = 6, Name = "Павлодар", Description = "Иртыш даёт воду и устойчивое развитие.", WaterBonus = 0.1f, EcoPoints = 5, MapTexture = "river", PrimaryColor = "#56ccf2" },
            new Region { Id = 7, Name = "ВКО", Description = "Горы и тайга помогают редким деревьям.", GrowthSpeedBonus = 0.1f, EnergyBonus = 0.1f, MapTexture = "forest", PrimaryColor = "#219653" },
            new Region { Id = 8, Name = "Мангистауская область", Description = "Суровый край с высокой энергией и риском засухи.", EnergyBonus = 0.3f, DroughtRisk = 0.25f, MapTexture = "caspian", PrimaryColor = "#f2994a" }
        );

        modelBuilder.Entity<TreeType>().HasData(
            new TreeType { Id = 1, Name = "Береза", Description = "Неприхотливое дерево для первых посадок.", Rarity = "Common", CostCoins = 30, CostWater = 15, CostEnergy = 5, UnlockLevel = 1, IsUnlockedByDefault = true, TimeToSapling = 1, TimeToSprout = 2, TimeToAdult = 4, EcoPointsProduction = 1, WaterProduction = 1, TrunkColor = "#f4ead2", LeafColor = "#7bc96f", BaseWidth = 58, BaseHeight = 82 },
            new TreeType { Id = 2, Name = "Тополь", Description = "Быстро растёт и хорошо подходит для степи.", Rarity = "Common", CostCoins = 25, CostWater = 10, CostEnergy = 5, UnlockLevel = 1, IsUnlockedByDefault = true, TimeToSapling = 1, TimeToSprout = 2, TimeToAdult = 3, EcoPointsProduction = 1, WaterProduction = 1, TrunkColor = "#9a7b55", LeafColor = "#2f8f46", BaseWidth = 48, BaseHeight = 100 },
            new TreeType { Id = 3, Name = "Осина", Description = "Лёгкое дерево с серебристой кроной.", Rarity = "Common", CostCoins = 35, CostWater = 20, CostEnergy = 8, UnlockLevel = 2, IsUnlockedByDefault = true, TimeToSapling = 1, TimeToSprout = 3, TimeToAdult = 5, EcoPointsProduction = 2, WaterProduction = 1, TrunkColor = "#b8b8a8", LeafColor = "#a3c853", BaseWidth = 56, BaseHeight = 74 },
            new TreeType { Id = 4, Name = "Дуб", Description = "Редкое сильное дерево с большим вкладом в экологию.", Rarity = "Rare", CostCoins = 80, CostWater = 40, CostEnergy = 20, UnlockLevel = 4, TimeToSapling = 2, TimeToSprout = 4, TimeToAdult = 7, EcoPointsProduction = 4, WaterProduction = 2, TrunkColor = "#5c4033", LeafColor = "#1f6f3a", BaseWidth = 96, BaseHeight = 104 },
            new TreeType { Id = 5, Name = "Кедр", Description = "Вечнозелёный редкий вид для сильной территории.", Rarity = "Rare", CostCoins = 100, CostWater = 50, CostEnergy = 25, UnlockLevel = 6, TimeToSapling = 2, TimeToSprout = 5, TimeToAdult = 8, EcoPointsProduction = 5, WaterProduction = 3, TrunkColor = "#4a3728", LeafColor = "#2e8b57", BaseWidth = 80, BaseHeight = 125 },
            new TreeType { Id = 6, Name = "Липа", Description = "Полезное медоносное дерево для городских парков.", Rarity = "Rare", CostCoins = 75, CostWater = 35, CostEnergy = 18, UnlockLevel = 5, TimeToSapling = 2, TimeToSprout = 4, TimeToAdult = 7, EcoPointsProduction = 3, WaterProduction = 2, TrunkColor = "#8b6914", LeafColor = "#86c95d", BaseWidth = 88, BaseHeight = 92 },
            new TreeType { Id = 7, Name = "Тянь-Шаньская ель", Description = "Эпическое горное дерево Казахстана.", Rarity = "Epic", CostCoins = 200, CostWater = 100, CostEnergy = 60, UnlockLevel = 10, TimeToSapling = 3, TimeToSprout = 6, TimeToAdult = 10, EcoPointsProduction = 8, WaterProduction = 5, TrunkColor = "#3b2f2f", LeafColor = "#1a6b3a", BaseWidth = 100, BaseHeight = 155 },
            new TreeType { Id = 8, Name = "Арча", Description = "Эпический можжевельник горных склонов.", Rarity = "Epic", CostCoins = 180, CostWater = 80, CostEnergy = 50, UnlockLevel = 9, TimeToSapling = 3, TimeToSprout = 6, TimeToAdult = 10, EcoPointsProduction = 7, WaterProduction = 4, TrunkColor = "#4b3832", LeafColor = "#355e3b", BaseWidth = 72, BaseHeight = 132 },
            new TreeType { Id = 9, Name = "Яблоня Сиверса", Description = "Легендарный предок культурных яблонь.", Rarity = "Legendary", CostCoins = 500, CostWater = 250, CostEnergy = 150, UnlockLevel = 15, TimeToSapling = 4, TimeToSprout = 8, TimeToAdult = 14, EcoPointsProduction = 15, WaterProduction = 8, TrunkColor = "#6b3a2a", LeafColor = "#2f9d53", BaseWidth = 126, BaseHeight = 132 },
            new TreeType { Id = 10, Name = "Реликтовая ива", Description = "Легендарное дерево древних пойменных лесов.", Rarity = "Legendary", CostCoins = 600, CostWater = 300, CostEnergy = 180, UnlockLevel = 20, TimeToSapling = 4, TimeToSprout = 8, TimeToAdult = 14, EcoPointsProduction = 20, WaterProduction = 12, TrunkColor = "#3d2b1f", LeafColor = "#78c850", BaseWidth = 160, BaseHeight = 122 }
        );

        modelBuilder.Entity<BuildingType>().HasData(
            new BuildingType { Id = 1, Name = "Склад воды", Description = "Увеличивает запас воды для полива.", Rarity = "Common", CostCoins = 100, CostEnergy = 30, WaterStorageBonus = 200, UnlockLevel = 1, MaxLevel = 5, BaseWidth = 96, BaseHeight = 80 },
            new BuildingType { Id = 2, Name = "Склад ресурсов", Description = "Хранит монеты, энергию и материалы.", Rarity = "Common", CostCoins = 150, CostEnergy = 40, ResourceStorageBonus = 500, UnlockLevel = 1, MaxLevel = 5, BaseWidth = 98, BaseHeight = 82 },
            new BuildingType { Id = 3, Name = "Питомник", Description = "Ускоряет рост деревьев и открывает редкие виды.", Rarity = "Rare", CostCoins = 300, CostEnergy = 100, EcoBonus = 5, UnlockLevel = 5, MaxLevel = 3, BaseWidth = 128, BaseHeight = 96 },
            new BuildingType { Id = 4, Name = "Экологическая лаборатория", Description = "Исследует болезни, засухи и новые деревья.", Rarity = "Epic", CostCoins = 800, CostEnergy = 300, ResearchBonus = 10, EcoBonus = 15, UnlockLevel = 10, MaxLevel = 3, BaseWidth = 160, BaseHeight = 128 },
            new BuildingType { Id = 5, Name = "Национальный ботанический центр", Description = "Легендарный центр восстановления природы Казахстана.", Rarity = "Legendary", CostCoins = 2000, CostEnergy = 800, ResearchBonus = 30, EcoBonus = 50, UnlockLevel = 18, MaxLevel = 1, BaseWidth = 210, BaseHeight = 180 }
        );

        modelBuilder.Entity<NPCType>().HasData(
            new NPCType { Id = 1, Name = "Волонтёр", Description = "Убирает мусор и свалки.", Sprite = "volunteer", Role = "Volunteer", CostCoins = 150, UnlockLevel = 2 },
            new NPCType { Id = 2, Name = "Лесник", Description = "Лечит больные деревья.", Sprite = "forester", Role = "Forester", CostCoins = 250, UnlockLevel = 5 },
            new NPCType { Id = 3, Name = "Эколог", Description = "Даёт бонус к экологии территории.", Sprite = "ecologist", Role = "Ecologist", CostCoins = 350, UnlockLevel = 8 },
            new NPCType { Id = 4, Name = "Пожарный", Description = "Тушит пожары на деревьях.", Sprite = "firefighter", Role = "Firefighter", CostCoins = 300, UnlockLevel = 6 }
        );

        modelBuilder.Entity<Achievement>().HasData(
            new Achievement { Id = 1, Name = "Первое дерево", Description = "Посадить первое дерево.", Icon = "tree", AchievementType = "TreesPlanted", TargetValue = 1, RewardCoins = 50, RewardExperience = 25 },
            new Achievement { Id = 2, Name = "Зелёный сад", Description = "Посадить 10 деревьев.", Icon = "forest", AchievementType = "TreesPlanted", TargetValue = 10, RewardCoins = 200, RewardExperience = 100 },
            new Achievement { Id = 3, Name = "Лес Казахстана", Description = "Посадить 100 деревьев.", Icon = "forest_big", AchievementType = "TreesPlanted", TargetValue = 100, RewardCoins = 1000, RewardExperience = 500 },
            new Achievement { Id = 4, Name = "Чистая территория", Description = "Убрать 50 свалок.", Icon = "trash", AchievementType = "TrashCleaned", TargetValue = 50, RewardCoins = 500, RewardExperience = 250 },
            new Achievement { Id = 5, Name = "Огнеборец", Description = "Потушить 10 пожаров.", Icon = "fire", AchievementType = "FiresExtinguished", TargetValue = 10, RewardCoins = 300, RewardExperience = 150 },
            new Achievement { Id = 6, Name = "Знаток природы", Description = "Пройти 20 вопросов викторины.", Icon = "quiz", AchievementType = "QuizCompleted", TargetValue = 20, RewardCoins = 200, RewardExperience = 200 },
            new Achievement { Id = 7, Name = "Легендарный садовод", Description = "Разблокировать легендарное дерево.", Icon = "legendary", AchievementType = "LegendaryUnlocked", TargetValue = 1, RewardCoins = 2000, RewardExperience = 1000 }
        );

        modelBuilder.Entity<QuizCategory>().HasData(
            new QuizCategory { Id = 1, Name = "Биология", Description = "Растения, деревья и животные.", Icon = "biology" },
            new QuizCategory { Id = 2, Name = "География Казахстана", Description = "Области, города, реки и горы.", Icon = "geography" },
            new QuizCategory { Id = 3, Name = "Экология", Description = "Переработка, загрязнение и климат.", Icon = "ecology" },
            new QuizCategory { Id = 4, Name = "Жасыл Ел", Description = "История, цели и проекты.", Icon = "jasyl-el" }
        );

        modelBuilder.Entity<QuizQuestion>().HasData(
            Q(1, 1, 1, "Какое дерево считается предком культурных яблонь?", "Берёза", "Тополь", "Яблоня Сиверса", "Дуб", 2, "Яблоня Сиверса растёт в горах Казахстана и считается предком многих яблонь.", 10, 15, 5, 5, 9),
            Q(2, 1, 1, "Сколько стадий роста у дерева в игре?", "2", "3", "4", "5", 2, "Семя, саженец, молодой росток и взрослое дерево.", 10, 15, 5, 5, null),
            Q(3, 1, 2, "Что выделяют деревья при фотосинтезе?", "CO2", "Кислород", "Азот", "Водород", 1, "Деревья поглощают углекислый газ и выделяют кислород.", 20, 30, 10, 8, null),
            Q(4, 1, 3, "Арча - это казахское название какого растения?", "Ель", "Можжевельник", "Сосна", "Пихта", 1, "Арча - можжевельник, важный для горных экосистем.", 35, 50, 15, 12, 8),
            Q(5, 2, 1, "Какой город является столицей Казахстана?", "Алматы", "Шымкент", "Астана", "Караганда", 2, "Астана является столицей Казахстана.", 10, 15, 5, 5, null),
            Q(6, 2, 1, "Какая река протекает через Павлодар?", "Иртыш", "Или", "Сырдарья", "Урал", 0, "Павлодар расположен на Иртыше.", 10, 15, 5, 5, null),
            Q(7, 2, 2, "Где растёт Тянь-Шаньская ель?", "В пустынях", "В горах Тянь-Шаня", "На Каспии", "В тундре", 1, "Тянь-Шаньская ель связана с горными экосистемами юго-востока Казахстана.", 20, 30, 10, 8, 7),
            Q(8, 2, 4, "Какая вершина известна как одна из высочайших точек Казахстана?", "Хан-Тенгри", "Бектау-Ата", "Кок-Тобе", "Бурабай", 0, "Хан-Тенгри - знаменитая высокая вершина Тянь-Шаня.", 50, 75, 20, 18, null),
            Q(9, 3, 1, "Что означает раздельный сбор мусора?", "Сжигание отходов", "Сортировка по материалам", "Вывоз ночью", "Захоронение всего мусора", 1, "Сортировка помогает перерабатывать пластик, стекло, бумагу и металл.", 10, 15, 5, 5, null),
            Q(10, 3, 2, "Какое событие замедляет рост деревьев в игре?", "Праздник", "Засуха", "Дождь", "Улучшение склада", 1, "Засуха ухудшает состояние земли и замедляет рост.", 20, 30, 10, 8, null),
            Q(11, 3, 3, "Какой газ чаще всего связывают с изменением климата?", "Кислород", "Гелий", "Углекислый газ", "Неон", 2, "CO2 является одним из главных парниковых газов.", 35, 50, 15, 12, null),
            Q(12, 3, 4, "Что даёт экологическая лаборатория?", "Исследования и бонус экологии", "Только монеты", "Только воду", "Случайный урон", 0, "Лаборатория помогает исследовать проблемы и развивать экологию.", 50, 75, 20, 18, null),
            Q(13, 4, 1, "Что означает 'Жасыл Ел'?", "Зелёная страна", "Синее небо", "Новая вода", "Большой город", 0, "Жасыл Ел переводится как зелёная страна.", 10, 15, 5, 5, null),
            Q(14, 4, 2, "Кто убирает мусор на карте?", "Волонтёр", "Лесник", "Эколог", "Пожарный", 0, "Волонтёр отвечает за мусор и свалки.", 20, 30, 10, 8, null),
            Q(15, 4, 3, "Кто тушит пожары?", "Эколог", "Пожарный", "Лесник", "Питомник", 1, "Пожарный помогает бороться с огнём.", 35, 50, 15, 12, null),
            Q(16, 4, 4, "Какой объект относится к легендарным постройкам?", "Склад воды", "Питомник", "Экологическая лаборатория", "Национальный ботанический центр", 3, "Национальный ботанический центр - легендарная постройка.", 50, 75, 20, 18, 10)
        );
    }

    private static QuizQuestion Q(int id, int categoryId, int difficulty, string text, string a, string b, string c, string d, int correct, string explanation, int coins, int exp, int water, int energy, int? treeId)
    {
        return new QuizQuestion
        {
            Id = id,
            CategoryId = categoryId,
            Difficulty = difficulty,
            QuestionText = text,
            AnswerOptions = System.Text.Json.JsonSerializer.Serialize(new[] { a, b, c, d }),
            CorrectAnswerIndex = correct,
            Explanation = explanation,
            RewardCoins = coins,
            RewardExperience = exp,
            RewardWater = water,
            RewardEnergy = energy,
            RewardTreeTypeId = treeId
        };
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using JasylEl.DTOs;
using JasylEl.Models;
using JasylEl.Repositories;
using Microsoft.IdentityModel.Tokens;

namespace JasylEl.Services
{
    // ===================== AUTH SERVICE =====================

    public interface IAuthService
    {
        Task<AuthResponseDto> RegisterAsync(RegisterDto dto);
        Task<AuthResponseDto> LoginAsync(LoginDto dto);
        string GenerateToken(User user);
    }

    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepo;
        private readonly ITreeRepository _treeRepo;
        private readonly IConfiguration _config;

        public AuthService(IUserRepository userRepo, ITreeRepository treeRepo, IConfiguration config)
        {
            _userRepo = userRepo;
            _treeRepo = treeRepo;
            _config = config;
        }

        public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
        {
            // Проверяем уникальность
            var existingEmail = await _userRepo.GetByEmailAsync(dto.Email);
            if (existingEmail != null)
                throw new InvalidOperationException("Email уже зарегистрирован");

            var existingUsername = await _userRepo.GetByUsernameAsync(dto.Username);
            if (existingUsername != null)
                throw new InvalidOperationException("Имя пользователя уже занято");

            var user = new User
            {
                Username = dto.Username,
                Email = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                RegionId = dto.RegionId,
                Level = 1,
                Experience = 0,
                ExperienceToNextLevel = 100,
                Coins = 500,
                Water = 200,
                Energy = 100
            };

            await _userRepo.CreateAsync(user);

            // Разблокируем дефолтные деревья (берёза, тополь, осина)
            var defaultTrees = new[] { 1, 2, 3 };
            foreach (var treeId in defaultTrees)
                await _treeRepo.UnlockTreeForUserAsync(user.Id, treeId);

            var token = GenerateToken(user);
            return new AuthResponseDto { Token = token, User = MapUserToDto(user) };
        }

        public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
        {
            var user = await _userRepo.GetByEmailOrUsernameAsync(dto.EmailOrUsername);
            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                throw new UnauthorizedAccessException("Неверный логин или пароль");

            user.LastLoginAt = DateTime.UtcNow;
            await _userRepo.UpdateAsync(user);

            var token = GenerateToken(user);
            return new AuthResponseDto { Token = token, User = MapUserToDto(user) };
        }

        public string GenerateToken(User user)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                _config["Jwt:Key"] ?? "JasylElSecretKey2024KazakhstanGreen!"));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email)
            };

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"] ?? "JasylEl",
                audience: _config["Jwt:Audience"] ?? "JasylElGame",
                claims: claims,
                expires: DateTime.UtcNow.AddDays(30),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static UserProfileDto MapUserToDto(User user) => new()
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            Level = user.Level,
            Experience = user.Experience,
            ExperienceToNextLevel = user.ExperienceToNextLevel,
            Coins = user.Coins,
            Water = user.Water,
            Energy = user.Energy,
            EcoPoints = user.EcoPoints,
            JasylElReputation = user.JasylElReputation,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
            Region = user.Region == null ? null : new RegionDto
            {
                Id = user.Region.Id,
                Name = user.Region.Name,
                Description = user.Region.Description,
                EnergyBonus = user.Region.EnergyBonus,
                WaterBonus = user.Region.WaterBonus,
                GrowthSpeedBonus = user.Region.GrowthSpeedBonus,
                CoinsBonus = user.Region.CoinsBonus,
                ExperienceBonus = user.Region.ExperienceBonus,
                DroughtRisk = user.Region.DroughtRisk,
                PrimaryColor = user.Region.PrimaryColor,
                MapTexture = user.Region.MapTexture
            }
        };
    }

    // ===================== GAME SERVICE =====================

    public interface IGameService
    {
        Task<MapDataDto> GetMapAsync(int userId);
        Task<MapObjectDto> PlantTreeAsync(int userId, PlantTreeDto dto);
        Task<MapObjectDto> PlaceBuildingAsync(int userId, PlaceBuildingDto dto);
        Task<MapObjectDto> UpgradeBuildingAsync(int userId, UpgradeBuildingDto dto);
        Task<UserProfileDto> GetProfileAsync(int userId);
        Task UpdateGrowthAsync();
        Task SpawnRandomEventAsync(int userId);
        Task<string> ResolveEventAsync(int userId, int eventId);
        Task<List<TreeTypeDto>> GetAvailableTreesAsync(int userId);
        Task<List<BuildingTypeDto>> GetAvailableBuildingsAsync(int userId);
        Task<List<LeaderboardEntryDto>> GetLeaderboardAsync();
    }

    public class GameService : IGameService
    {
        private readonly IUserRepository _userRepo;
        private readonly IMapRepository _mapRepo;
        private readonly ITreeRepository _treeRepo;
        private readonly IBuildingRepository _buildingRepo;
        private readonly IAchievementRepository _achievementRepo;
        private readonly IEventRepository _eventRepo;
        private readonly INPCRepository _npcRepo;
        private readonly Random _random = new();

        public GameService(
            IUserRepository userRepo, IMapRepository mapRepo,
            ITreeRepository treeRepo, IBuildingRepository buildingRepo,
            IAchievementRepository achievementRepo,
            IEventRepository eventRepo, INPCRepository npcRepo)
        {
            _userRepo = userRepo;
            _mapRepo = mapRepo;
            _treeRepo = treeRepo;
            _buildingRepo = buildingRepo;
            _achievementRepo = achievementRepo;
            _eventRepo = eventRepo;
            _npcRepo = npcRepo;
        }

        /// <summary>Получить все объекты карты пользователя</summary>
        public async Task<MapDataDto> GetMapAsync(int userId)
        {
            var objects = await _mapRepo.GetUserMapObjectsAsync(userId);
            var events = await _eventRepo.GetActiveUserEventsAsync(userId);
            var npcs = await _npcRepo.GetUserNPCsAsync(userId);

            // Обновляем стадии роста деревьев
            bool changed = false;
            foreach (var obj in objects.Where(o => o.ObjectType == "Tree"))
            {
                if (obj.NextGrowthAt <= DateTime.UtcNow && obj.GrowthStage != "Adult")
                {
                    AdvanceGrowthStage(obj);
                    changed = true;
                }
            }
            if (changed)
                foreach (var obj in objects.Where(o => o.ObjectType == "Tree"))
                    await _mapRepo.UpdateAsync(obj);

            return new MapDataDto
            {
                Objects = objects.Select(MapObjectToDto).ToList(),
                Events = events.Select(e => new EcoEventDto
                {
                    Id = e.Id, EventType = e.EventType,
                    PositionX = e.PositionX, PositionY = e.PositionY,
                    IsResolved = e.IsResolved, CreatedAt = e.CreatedAt,
                    MapObjectId = e.MapObjectId
                }).ToList(),
                NPCs = npcs.Select(n => new NPCDto
                {
                    Id = n.Id, Name = n.NPCType?.Name ?? "",
                    Role = n.NPCType?.Role ?? "", Sprite = n.NPCType?.Sprite ?? "",
                    PositionX = n.PositionX, PositionY = n.PositionY,
                    TargetX = n.TargetX, TargetY = n.TargetY
                }).ToList()
            };
        }

        /// <summary>Посадить дерево на карте</summary>
        public async Task<MapObjectDto> PlantTreeAsync(int userId, PlantTreeDto dto)
        {
            var user = await _userRepo.GetByIdAsync(userId)
                ?? throw new InvalidOperationException("Пользователь не найден");

            var treeType = await _treeRepo.GetByIdAsync(dto.TreeTypeId)
                ?? throw new InvalidOperationException("Тип дерева не найден");

            // Проверяем разблокировку
            bool unlocked = treeType.IsUnlockedByDefault ||
                await _treeRepo.IsTreeUnlockedForUserAsync(userId, dto.TreeTypeId);
            if (!unlocked)
                throw new InvalidOperationException("Это дерево ещё не разблокировано");

            // Проверяем уровень
            if (user.Level < treeType.UnlockLevel)
                throw new InvalidOperationException($"Недостаточный уровень. Нужен уровень {treeType.UnlockLevel}");

            // Проверяем ресурсы
            if (user.Coins < treeType.CostCoins || user.Water < treeType.CostWater || user.Energy < treeType.CostEnergy)
                throw new InvalidOperationException("Недостаточно ресурсов");

            // Списываем ресурсы
            user.Coins -= treeType.CostCoins;
            user.Water -= treeType.CostWater;
            user.Energy -= treeType.CostEnergy;

            // Начисляем опыт и экологические очки
            int expGain = 10 + (treeType.Rarity switch { "Rare" => 20, "Epic" => 50, "Legendary" => 100, _ => 0 });
            await AddExperienceAsync(user, expGain);
            user.EcoPoints += treeType.EcoPointsProduction;

            await _userRepo.UpdateAsync(user);

            // Создаём объект на карте
            var region = user.Region;
            float growthSpeedMultiplier = region != null ? 1f + region.GrowthSpeedBonus : 1f;
            int timeToSapling = (int)(treeType.TimeToSapling / growthSpeedMultiplier);

            var mapObj = new MapObject
            {
                UserId = userId,
                ObjectType = "Tree",
                TreeTypeId = dto.TreeTypeId,
                PositionX = dto.PositionX,
                PositionY = dto.PositionY,
                GrowthStage = "Seed",
                PlantedAt = DateTime.UtcNow,
                NextGrowthAt = DateTime.UtcNow.AddMinutes(timeToSapling)
            };

            await _mapRepo.CreateAsync(mapObj);

            // Обновляем достижения
            int treesPlanted = await _userRepo.GetTreesPlantedCountAsync(userId);
            await UpdateAchievementProgressAsync(userId, "TreesPlanted", treesPlanted);

            return MapObjectToDto(mapObj);
        }

        /// <summary>Разместить постройку</summary>
        public async Task<MapObjectDto> PlaceBuildingAsync(int userId, PlaceBuildingDto dto)
        {
            var user = await _userRepo.GetByIdAsync(userId)
                ?? throw new InvalidOperationException("Пользователь не найден");

            var buildingType = await _buildingRepo.GetByIdAsync(dto.BuildingTypeId)
                ?? throw new InvalidOperationException("Тип постройки не найден");

            if (user.Level < buildingType.UnlockLevel)
                throw new InvalidOperationException($"Нужен уровень {buildingType.UnlockLevel}");

            if (user.Coins < buildingType.CostCoins || user.Energy < buildingType.CostEnergy)
                throw new InvalidOperationException("Недостаточно ресурсов");

            user.Coins -= buildingType.CostCoins;
            user.Energy -= buildingType.CostEnergy;
            await AddExperienceAsync(user, 15);
            await _userRepo.UpdateAsync(user);

            var mapObj = new MapObject
            {
                UserId = userId,
                ObjectType = "Building",
                BuildingTypeId = dto.BuildingTypeId,
                PositionX = dto.PositionX,
                PositionY = dto.PositionY,
                BuildingLevel = 1,
                GrowthStage = "Adult",
                PlantedAt = DateTime.UtcNow
            };

            await _mapRepo.CreateAsync(mapObj);
            return MapObjectToDto(mapObj);
        }

        /// <summary>Улучшить постройку</summary>
        public async Task<MapObjectDto> UpgradeBuildingAsync(int userId, UpgradeBuildingDto dto)
        {
            var mapObj = await _mapRepo.GetByIdAsync(dto.MapObjectId)
                ?? throw new InvalidOperationException("Объект не найден");

            if (mapObj.UserId != userId)
                throw new UnauthorizedAccessException("Это не ваш объект");

            var buildingType = await _buildingRepo.GetByIdAsync(mapObj.BuildingTypeId ?? 0)
                ?? throw new InvalidOperationException("Тип постройки не найден");

            if (mapObj.BuildingLevel >= buildingType.MaxLevel)
                throw new InvalidOperationException("Постройка уже на максимальном уровне");

            var user = await _userRepo.GetByIdAsync(userId)!;
            int upgradeCost = buildingType.CostCoins * mapObj.BuildingLevel;

            if (user!.Coins < upgradeCost)
                throw new InvalidOperationException("Недостаточно монет для улучшения");

            user.Coins -= upgradeCost;
            mapObj.BuildingLevel++;
            await _userRepo.UpdateAsync(user);
            await _mapRepo.UpdateAsync(mapObj);

            return MapObjectToDto(mapObj);
        }

        /// <summary>Получить профиль пользователя</summary>
        public async Task<UserProfileDto> GetProfileAsync(int userId)
        {
            var user = await _userRepo.GetByIdAsync(userId)
                ?? throw new InvalidOperationException("Пользователь не найден");

            return new UserProfileDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Level = user.Level,
                Experience = user.Experience,
                ExperienceToNextLevel = user.ExperienceToNextLevel,
                Coins = user.Coins,
                Water = user.Water,
                Energy = user.Energy,
                EcoPoints = user.EcoPoints,
                JasylElReputation = user.JasylElReputation,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt,
                Region = user.Region == null ? null : new RegionDto
                {
                    Id = user.Region.Id, Name = user.Region.Name,
                    Description = user.Region.Description,
                    EnergyBonus = user.Region.EnergyBonus,
                    WaterBonus = user.Region.WaterBonus,
                    GrowthSpeedBonus = user.Region.GrowthSpeedBonus,
                    CoinsBonus = user.Region.CoinsBonus,
                    ExperienceBonus = user.Region.ExperienceBonus,
                    DroughtRisk = user.Region.DroughtRisk,
                    PrimaryColor = user.Region.PrimaryColor,
                    MapTexture = user.Region.MapTexture
                }
            };
        }

        /// <summary>Обновить стадии роста всех деревьев (фоновая задача)</summary>
        public async Task UpdateGrowthAsync()
        {
            // Этот метод вызывается периодически для обновления всех деревьев
            // В production это был бы Hangfire/Quartz job
        }

        /// <summary>Создать случайное экологическое событие</summary>
        public async Task SpawnRandomEventAsync(int userId)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null) return;

            var eventTypes = new[] { "Fire", "Drought", "Trash", "Disease" };
            var eventType = eventTypes[_random.Next(eventTypes.Length)];

            // Случайная позиция на карте 3000x3000
            float x = (float)(_random.NextDouble() * 2800 + 100);
            float y = (float)(_random.NextDouble() * 2800 + 100);

            // Засуха чаще в засушливых регионах
            if (user.Region?.DroughtRisk > 0.2f && _random.NextDouble() < user.Region.DroughtRisk)
                eventType = "Drought";

            var evt = new EcoEvent
            {
                UserId = userId,
                EventType = eventType,
                PositionX = x,
                PositionY = y
            };

            await _eventRepo.CreateAsync(evt);
        }

        /// <summary>Решить экологическое событие</summary>
        public async Task<string> ResolveEventAsync(int userId, int eventId)
        {
            await _eventRepo.ResolveAsync(eventId);

            var user = await _userRepo.GetByIdAsync(userId)!;
            user!.Coins += 20;
            user.EcoPoints += 5;
            user.JasylElReputation += 2;
            await AddExperienceAsync(user, 15);
            await _userRepo.UpdateAsync(user);

            await UpdateAchievementProgressAsync(userId, "TrashCleaned", 1);

            return "Событие успешно устранено!";
        }

        /// <summary>Получить доступные деревья для пользователя</summary>
        public async Task<List<TreeTypeDto>> GetAvailableTreesAsync(int userId)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            var allTrees = await _treeRepo.GetAllAsync();
            var unlockedTrees = await _treeRepo.GetUserUnlockedTreesAsync(userId);
            var unlockedIds = unlockedTrees.Select(u => u.TreeTypeId).ToHashSet();

            return allTrees.Select(t => new TreeTypeDto
            {
                Id = t.Id, Name = t.Name, Description = t.Description,
                Rarity = t.Rarity, CostCoins = t.CostCoins, CostWater = t.CostWater,
                CostEnergy = t.CostEnergy, UnlockLevel = t.UnlockLevel,
                IsUnlocked = t.IsUnlockedByDefault || unlockedIds.Contains(t.Id),
                EcoPointsProduction = t.EcoPointsProduction,
                WaterProduction = t.WaterProduction,
                BaseWidth = t.BaseWidth, BaseHeight = t.BaseHeight,
                TrunkColor = t.TrunkColor, LeafColor = t.LeafColor
            }).ToList();
        }

        /// <summary>Получить доступные постройки</summary>
        public async Task<List<BuildingTypeDto>> GetAvailableBuildingsAsync(int userId)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            var buildings = await _buildingRepo.GetAllAsync();

            return buildings.Select(b => new BuildingTypeDto
            {
                Id = b.Id, Name = b.Name, Description = b.Description,
                Rarity = b.Rarity, CostCoins = b.CostCoins, CostEnergy = b.CostEnergy,
                MaxLevel = b.MaxLevel, UnlockLevel = b.UnlockLevel,
                IsUnlocked = (user?.Level ?? 0) >= b.UnlockLevel,
                BaseWidth = b.BaseWidth, BaseHeight = b.BaseHeight
            }).ToList();
        }

        /// <summary>Получить рейтинг игроков</summary>
        public async Task<List<LeaderboardEntryDto>> GetLeaderboardAsync()
        {
            var users = await _userRepo.GetLeaderboardAsync(20);
            var result = new List<LeaderboardEntryDto>();

            for (int i = 0; i < users.Count; i++)
            {
                var u = users[i];
                int trees = await _userRepo.GetTreesPlantedCountAsync(u.Id);
                result.Add(new LeaderboardEntryDto
                {
                    Rank = i + 1,
                    Username = u.Username,
                    Level = u.Level,
                    EcoPoints = u.EcoPoints,
                    TreesPlanted = trees,
                    RegionName = u.Region?.Name ?? "Неизвестно"
                });
            }

            return result;
        }

        // ===================== ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ =====================

        private static void AdvanceGrowthStage(MapObject obj)
        {
            obj.GrowthStage = obj.GrowthStage switch
            {
                "Seed" => "Sapling",
                "Sapling" => "Sprout",
                "Sprout" => "Adult",
                _ => "Adult"
            };
            // Следующий рост через удвоенное время
            obj.NextGrowthAt = DateTime.UtcNow.AddMinutes(30);
        }

        private async Task AddExperienceAsync(User user, int amount)
        {
            user.Experience += amount;

            // Проверяем повышение уровня
            while (user.Experience >= user.ExperienceToNextLevel && user.Level < 50)
            {
                user.Experience -= user.ExperienceToNextLevel;
                user.Level++;
                user.ExperienceToNextLevel = CalculateExpRequired(user.Level);

                // Бонус за уровень
                user.Coins += 100 * user.Level;
                user.Energy += 50;
            }
        }

        private static int CalculateExpRequired(int level) => 100 + (level - 1) * 50;

        private async Task UpdateAchievementProgressAsync(int userId, string type, int value)
        {
            var allAchievements = await _achievementRepo.GetAllAsync();
            var relevant = allAchievements.Where(a => a.AchievementType == type);

            foreach (var achievement in relevant)
                await _achievementRepo.CreateOrUpdateUserAchievementAsync(userId, achievement.Id, value);
        }

        private static MapObjectDto MapObjectToDto(MapObject obj)
        {
            var dto = new MapObjectDto
            {
                Id = obj.Id,
                ObjectType = obj.ObjectType,
                TreeTypeId = obj.TreeTypeId,
                BuildingTypeId = obj.BuildingTypeId,
                PositionX = obj.PositionX,
                PositionY = obj.PositionY,
                GrowthStage = obj.GrowthStage,
                BuildingLevel = obj.BuildingLevel,
                Health = obj.Health,
                IsOnFire = obj.IsOnFire,
                IsDiseased = obj.IsDiseased,
                IsDry = obj.IsDry,
                PlantedAt = obj.PlantedAt,
                NextGrowthAt = obj.NextGrowthAt
            };

            if (obj.TreeType != null)
            {
                dto.TreeName = obj.TreeType.Name;
                dto.TreeRarity = obj.TreeType.Rarity;
                dto.TrunkColor = obj.TreeType.TrunkColor;
                dto.LeafColor = obj.TreeType.LeafColor;
                dto.Width = obj.TreeType.BaseWidth * GetGrowthScale(obj.GrowthStage);
                dto.Height = obj.TreeType.BaseHeight * GetGrowthScale(obj.GrowthStage);
            }

            if (obj.BuildingType != null)
            {
                dto.BuildingName = obj.BuildingType.Name;
                dto.Width = obj.BuildingType.BaseWidth;
                dto.Height = obj.BuildingType.BaseHeight;
            }

            return dto;
        }

        private static float GetGrowthScale(string stage) => stage switch
        {
            "Seed" => 0.3f,
            "Sapling" => 0.5f,
            "Sprout" => 0.75f,
            "Adult" => 1.0f,
            _ => 1.0f
        };
    }

    // ===================== QUIZ SERVICE =====================

    public interface IQuizService
    {
        Task<List<QuizCategoryDto>> GetCategoriesAsync(int userId);
        Task<List<QuizQuestionDto>> GetQuestionsAsync(int categoryId, int difficulty = 0);
        Task<QuizResultDto> SubmitAnswerAsync(int userId, SubmitAnswerDto dto);
        Task<List<AchievementDto>> GetUserAchievementsAsync(int userId);
    }

    public class QuizService : IQuizService
    {
        private readonly IQuizRepository _quizRepo;
        private readonly IUserRepository _userRepo;
        private readonly IAchievementRepository _achievementRepo;
        private readonly ITreeRepository _treeRepo;

        public QuizService(IQuizRepository quizRepo, IUserRepository userRepo,
            IAchievementRepository achievementRepo, ITreeRepository treeRepo)
        {
            _quizRepo = quizRepo;
            _userRepo = userRepo;
            _achievementRepo = achievementRepo;
            _treeRepo = treeRepo;
        }

        public async Task<List<QuizCategoryDto>> GetCategoriesAsync(int userId)
        {
            var categories = await _quizRepo.GetCategoriesAsync();
            var userResults = await _quizRepo.GetUserResultsAsync(userId);
            var answeredIds = userResults.Where(r => r.IsCorrect).Select(r => r.QuestionId).ToHashSet();

            var result = new List<QuizCategoryDto>();
            foreach (var cat in categories)
            {
                var questions = await _quizRepo.GetQuestionsByCategoryAsync(cat.Id);
                result.Add(new QuizCategoryDto
                {
                    Id = cat.Id, Name = cat.Name, Description = cat.Description, Icon = cat.Icon,
                    TotalQuestions = questions.Count,
                    CompletedByUser = questions.Count(q => answeredIds.Contains(q.Id))
                });
            }
            return result;
        }

        public async Task<List<QuizQuestionDto>> GetQuestionsAsync(int categoryId, int difficulty = 0)
        {
            var questions = await _quizRepo.GetQuestionsByCategoryAsync(categoryId, difficulty);
            return questions.Select(q => new QuizQuestionDto
            {
                Id = q.Id, CategoryId = q.CategoryId,
                CategoryName = q.Category?.Name ?? "",
                QuestionText = q.QuestionText,
                Difficulty = q.Difficulty,
                AnswerOptions = JsonSerializer.Deserialize<List<string>>(q.AnswerOptions) ?? new(),
                RewardCoins = q.RewardCoins,
                RewardExperience = q.RewardExperience,
                RewardWater = q.RewardWater,
                RewardEnergy = q.RewardEnergy
            }).ToList();
        }

        public async Task<QuizResultDto> SubmitAnswerAsync(int userId, SubmitAnswerDto dto)
        {
            var question = await _quizRepo.GetQuestionByIdAsync(dto.QuestionId)
                ?? throw new InvalidOperationException("Вопрос не найден");

            var user = await _userRepo.GetByIdAsync(userId)
                ?? throw new InvalidOperationException("Пользователь не найден");

            bool isCorrect = dto.AnswerIndex == question.CorrectAnswerIndex;

            // Сохраняем результат
            await _quizRepo.SaveResultAsync(new QuizResult
            {
                UserId = userId,
                QuestionId = dto.QuestionId,
                AnswerIndex = dto.AnswerIndex,
                IsCorrect = isCorrect
            });

            var resultDto = new QuizResultDto
            {
                IsCorrect = isCorrect,
                Explanation = question.Explanation,
                NewLevel = user.Level,
                NewExperience = user.Experience
            };

            if (isCorrect)
            {
                // Начисляем награды с учётом бонуса региона
                float expBonus = user.Region?.ExperienceBonus ?? 0;
                float coinsBonus = user.Region?.CoinsBonus ?? 0;

                int rewardExp = (int)(question.RewardExperience * (1 + expBonus));
                int rewardCoins = (int)(question.RewardCoins * (1 + coinsBonus));

                user.Coins += rewardCoins;
                user.Water += question.RewardWater;
                user.Energy += question.RewardEnergy;
                user.EcoPoints += 3;

                // Добавляем опыт
                user.Experience += rewardExp;
                while (user.Experience >= user.ExperienceToNextLevel && user.Level < 50)
                {
                    user.Experience -= user.ExperienceToNextLevel;
                    user.Level++;
                    user.ExperienceToNextLevel = 100 + (user.Level - 1) * 50;
                    user.Coins += 100 * user.Level;
                }

                // Разблокировка дерева за викторину
                if (question.RewardTreeTypeId.HasValue)
                {
                    await _treeRepo.UnlockTreeForUserAsync(userId, question.RewardTreeTypeId.Value);
                    var tree = await _treeRepo.GetByIdAsync(question.RewardTreeTypeId.Value);
                    resultDto.UnlockedTree = tree?.Name;
                }

                await _userRepo.UpdateAsync(user);

                resultDto.RewardedCoins = rewardCoins;
                resultDto.RewardedExperience = rewardExp;
                resultDto.RewardedWater = question.RewardWater;
                resultDto.RewardedEnergy = question.RewardEnergy;
                resultDto.NewLevel = user.Level;
                resultDto.NewExperience = user.Experience;

                // Обновляем достижения
                int quizCount = await _quizRepo.GetUserCorrectAnswersCountAsync(userId);
                var allAchievements = await _achievementRepo.GetAllAsync();
                foreach (var a in allAchievements.Where(a => a.AchievementType == "QuizCompleted"))
                    await _achievementRepo.CreateOrUpdateUserAchievementAsync(userId, a.Id, quizCount);

                var newAchievements = await _achievementRepo.GetNewlyCompletedAsync(userId);
                resultDto.NewAchievements = newAchievements.Select(a => a.Name).ToList();
            }

            return resultDto;
        }

        public async Task<List<AchievementDto>> GetUserAchievementsAsync(int userId)
        {
            var allAchievements = await _achievementRepo.GetAllAsync();
            var userAchievements = await _achievementRepo.GetUserAchievementsAsync(userId);
            var userDict = userAchievements.ToDictionary(a => a.AchievementId);

            return allAchievements.Select(a =>
            {
                userDict.TryGetValue(a.Id, out var ua);
                return new AchievementDto
                {
                    Id = a.Id, Name = a.Name, Description = a.Description,
                    Icon = a.Icon, AchievementType = a.AchievementType,
                    TargetValue = a.TargetValue,
                    CurrentProgress = ua?.CurrentProgress ?? 0,
                    IsCompleted = ua?.IsCompleted ?? false,
                    CompletedAt = ua?.CompletedAt,
                    RewardCoins = a.RewardCoins,
                    RewardExperience = a.RewardExperience
                };
            }).ToList();
        }
    }
}

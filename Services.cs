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
        Task<CanPlantResultDto> CanPlantAtAsync(int userId, float x, float y, int treeTypeId);
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
        private const float MapSize = 3000f;
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

        private sealed record ZoneDefinition(
            int Id,
            string ZoneType,
            string Name,
            float X,
            float Y,
            float Width,
            float Height,
            bool CanPlant,
            float GrowthMultiplier,
            float WaterCostMultiplier,
            float DroughtRiskModifier,
            string Visual);

        /// <summary>Получить все объекты карты пользователя</summary>
        public async Task<MapDataDto> GetMapAsync(int userId)
        {
            var user = await _userRepo.GetByIdAsync(userId)
                ?? throw new InvalidOperationException("Пользователь не найден");
            var objects = await _mapRepo.GetUserMapObjectsAsync(userId);
            var events = await _eventRepo.GetActiveUserEventsAsync(userId);
            var npcs = await _npcRepo.GetUserNPCsAsync(userId);
            var zones = BuildZonesForRegion(user.RegionId);

            // Обновляем стадии роста деревьев
            bool changed = false;
            foreach (var obj in objects.Where(o => o.ObjectType == "Tree"))
            {
                if (obj.NextGrowthAt <= DateTime.UtcNow && obj.GrowthStage != "Adult")
                {
                    AdvanceGrowthStage(obj, user, zones);
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
                Zones = zones.Select(ToZoneDto).ToList(),
                NPCs = npcs.Select(n => new NPCDto
                {
                    Id = n.Id, Name = n.NPCType?.Name ?? "",
                    Role = n.NPCType?.Role ?? "", Sprite = n.NPCType?.Sprite ?? "",
                    PositionX = n.PositionX, PositionY = n.PositionY,
                    TargetX = n.TargetX, TargetY = n.TargetY
                }).ToList()
            };
        }

        public async Task<CanPlantResultDto> CanPlantAtAsync(int userId, float x, float y, int treeTypeId)
        {
            var user = await _userRepo.GetByIdAsync(userId)
                ?? throw new InvalidOperationException("Пользователь не найден");
            var treeType = await _treeRepo.GetByIdAsync(treeTypeId)
                ?? throw new InvalidOperationException("Тип дерева не найден");

            var zones = BuildZonesForRegion(user.RegionId);
            var zone = ResolveZone(x, y, zones);
            var waterMultiplier = GetWaterCostMultiplier(zone, x, y, zones);
            var growthMultiplier = GetGrowthMultiplier(user.Region, treeType, zone, x, y, zones);

            if (x < 0 || y < 0 || x > MapSize || y > MapSize)
            {
                return new CanPlantResultDto
                {
                    CanPlant = false,
                    Reason = "Точка вне границ карты.",
                    ZoneType = "OutOfBounds",
                    GrowthMultiplier = growthMultiplier,
                    WaterCostMultiplier = waterMultiplier
                };
            }

            if (!zone.CanPlant)
            {
                return new CanPlantResultDto
                {
                    CanPlant = false,
                    Reason = zone.ZoneType switch
                    {
                        "City" => "В городах сажать нельзя. Выберите участок за пределами города.",
                        "Water" => "На водных объектах посадка невозможна.",
                        _ => "В этой зоне посадка запрещена."
                    },
                    ZoneType = zone.ZoneType,
                    GrowthMultiplier = growthMultiplier,
                    WaterCostMultiplier = waterMultiplier
                };
            }

            return new CanPlantResultDto
            {
                CanPlant = true,
                Reason = $"Посадка разрешена: {zone.Name}.",
                ZoneType = zone.ZoneType,
                GrowthMultiplier = growthMultiplier,
                WaterCostMultiplier = waterMultiplier
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

            var zones = BuildZonesForRegion(user.RegionId);
            var canPlant = await CanPlantAtAsync(userId, dto.PositionX, dto.PositionY, dto.TreeTypeId);
            if (!canPlant.CanPlant)
                throw new InvalidOperationException(canPlant.Reason);

            int waterCost = (int)Math.Ceiling(treeType.CostWater * canPlant.WaterCostMultiplier);

            // Проверяем ресурсы
            if (user.Coins < treeType.CostCoins || user.Water < waterCost || user.Energy < treeType.CostEnergy)
                throw new InvalidOperationException("Недостаточно ресурсов");

            // Списываем ресурсы
            user.Coins -= treeType.CostCoins;
            user.Water -= waterCost;
            user.Energy -= treeType.CostEnergy;

            // Начисляем опыт и экологические очки
            int expGain = 10 + (treeType.Rarity switch { "Rare" => 20, "Epic" => 50, "Legendary" => 100, _ => 0 });
            await AddExperienceAsync(user, expGain);
            user.EcoPoints += treeType.EcoPointsProduction;

            await _userRepo.UpdateAsync(user);

            // Создаём объект на карте
            int timeToSapling = CalculateStageMinutes(treeType, "Seed", user, ResolveZone(dto.PositionX, dto.PositionY, zones), dto.PositionX, dto.PositionY, zones);

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
            var zones = BuildZonesForRegion(user.RegionId);

            // Случайная позиция на карте 3000x3000
            float x = (float)(_random.NextDouble() * 2800 + 100);
            float y = (float)(_random.NextDouble() * 2800 + 100);
            var zone = ResolveZone(x, y, zones);

            // Засуха чаще в засушливых регионах
            float droughtRisk = (user.Region?.DroughtRisk ?? 0.1f) + zone.DroughtRiskModifier;
            if (IsNearWater(x, y, zones))
                droughtRisk -= 0.08f;
            droughtRisk = Math.Clamp(droughtRisk, 0.03f, 0.85f);
            if (_random.NextDouble() < droughtRisk)
                eventType = "Drought";
            else if (zone.ZoneType == "Water" && _random.NextDouble() < 0.5f)
                eventType = "Trash";

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

        private static void AdvanceGrowthStage(MapObject obj, User user, List<ZoneDefinition> zones)
        {
            obj.GrowthStage = obj.GrowthStage switch
            {
                "Seed" => "Sapling",
                "Sapling" => "Sprout",
                "Sprout" => "Adult",
                _ => "Adult"
            };
            if (obj.GrowthStage == "Adult")
            {
                obj.NextGrowthAt = DateTime.UtcNow.AddYears(10);
                return;
            }

            var zone = ResolveZone(obj.PositionX, obj.PositionY, zones);
            int minutes = CalculateStageMinutes(obj.TreeType, obj.GrowthStage, user, zone, obj.PositionX, obj.PositionY, zones);
            obj.NextGrowthAt = DateTime.UtcNow.AddMinutes(minutes);
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

        private static List<ZoneDefinition> BuildZonesForRegion(int regionId)
        {
            var zones = new List<ZoneDefinition>
            {
                new(1, "Steppe", "Центральная степь", 0, 0, 3000, 3000, true, 1f, 1f, 0.02f, "steppe")
            };

            zones.Add(regionId switch
            {
                1 => new ZoneDefinition(2, "City", "Астана", 1320, 1230, 380, 360, false, 0.8f, 1.2f, 0.01f, "city"),
                2 => new ZoneDefinition(2, "City", "Алматы", 1950, 2140, 360, 340, false, 0.9f, 1.15f, 0.03f, "city"),
                3 => new ZoneDefinition(2, "City", "Шымкент", 1530, 2300, 340, 320, false, 0.9f, 1.15f, 0.03f, "city"),
                4 => new ZoneDefinition(2, "City", "Караганда", 1280, 1520, 320, 300, false, 0.85f, 1.2f, 0.04f, "city"),
                5 => new ZoneDefinition(2, "City", "Костанай", 900, 1050, 320, 300, false, 0.88f, 1.15f, 0.02f, "city"),
                6 => new ZoneDefinition(2, "City", "Павлодар", 1780, 1120, 330, 310, false, 0.88f, 1.1f, 0.01f, "city"),
                7 => new ZoneDefinition(2, "City", "Өскемен", 2380, 1090, 320, 300, false, 0.88f, 1.1f, 0.01f, "city"),
                8 => new ZoneDefinition(2, "City", "Актау", 380, 1770, 320, 300, false, 0.86f, 1.25f, 0.08f, "city"),
                _ => new ZoneDefinition(2, "City", "Город", 1400, 1300, 320, 300, false, 0.88f, 1.15f, 0.03f, "city")
            });

            zones.Add(regionId switch
            {
                8 => new ZoneDefinition(3, "Desert", "Сухие песчаные участки", 120, 1320, 980, 1260, true, 0.7f, 1.45f, 0.22f, "sand"),
                3 => new ZoneDefinition(3, "Desert", "Южная засушливая зона", 1200, 2250, 950, 620, true, 0.8f, 1.3f, 0.16f, "sand"),
                _ => new ZoneDefinition(3, "Dry", "Сухая зона", 260, 1860, 760, 680, true, 0.9f, 1.2f, 0.1f, "dry")
            });

            zones.Add(regionId switch
            {
                6 => new ZoneDefinition(4, "Water", "Озеро и пойма Иртыша", 1680, 830, 620, 620, false, 0.95f, 0.75f, -0.06f, "water"),
                2 => new ZoneDefinition(4, "Water", "Капчагайские водные зоны", 2060, 1960, 520, 440, false, 0.95f, 0.8f, -0.05f, "water"),
                8 => new ZoneDefinition(4, "Water", "Побережье Каспия", 0, 1620, 600, 760, false, 0.9f, 0.82f, -0.04f, "water"),
                _ => new ZoneDefinition(4, "Water", "Озёрная зона", 2250, 510, 470, 420, false, 0.95f, 0.8f, -0.05f, "water")
            });

            zones.Add(regionId switch
            {
                7 => new ZoneDefinition(5, "Forest", "Горный лесной пояс", 2150, 740, 780, 740, true, 1.22f, 0.9f, -0.04f, "forest"),
                2 => new ZoneDefinition(5, "Forest", "Предгорный зелёный пояс", 1700, 1830, 820, 740, true, 1.16f, 0.95f, -0.03f, "forest"),
                _ => new ZoneDefinition(5, "Fertile", "Плодородная зона", 1120, 700, 780, 650, true, 1.12f, 0.95f, -0.02f, "fertile")
            });

            return zones;
        }

        private static ZoneDefinition ResolveZone(float x, float y, List<ZoneDefinition> zones)
        {
            for (int i = zones.Count - 1; i >= 0; i--)
            {
                var z = zones[i];
                if (x >= z.X && x <= z.X + z.Width && y >= z.Y && y <= z.Y + z.Height)
                    return z;
            }
            return zones[0];
        }

        private static MapZoneDto ToZoneDto(ZoneDefinition zone) => new()
        {
            Id = zone.Id,
            ZoneType = zone.ZoneType,
            Name = zone.Name,
            X = zone.X,
            Y = zone.Y,
            Width = zone.Width,
            Height = zone.Height,
            CanPlant = zone.CanPlant,
            GrowthMultiplier = zone.GrowthMultiplier,
            WaterCostMultiplier = zone.WaterCostMultiplier,
            DroughtRiskModifier = zone.DroughtRiskModifier,
            Visual = zone.Visual
        };

        private static int CalculateStageMinutes(TreeType? treeType, string currentStage, User user, ZoneDefinition zone, float x, float y, List<ZoneDefinition> zones)
        {
            float baseMinutes = treeType?.Rarity switch
            {
                "Rare" => 2.6f,
                "Epic" => 3.3f,
                "Legendary" => 4.2f,
                _ => 2f
            };

            float treeMultiplier = treeType?.GrowthTimeMultiplier ?? 1f;
            float stageMultiplier = currentStage switch
            {
                "Seed" => 1f,
                "Sapling" => 1.05f,
                "Sprout" => 1.12f,
                _ => 1f
            };

            float regionMultiplier = 1f - (user.Region?.GrowthSpeedBonus ?? 0);
            if (regionMultiplier < 0.6f) regionMultiplier = 0.6f;

            float zoneMultiplier = 1f / Math.Max(0.35f, zone.GrowthMultiplier);
            if (IsNearWater(x, y, zones))
                zoneMultiplier *= 0.9f;

            int minutes = (int)Math.Round(baseMinutes * treeMultiplier * stageMultiplier * regionMultiplier * zoneMultiplier, MidpointRounding.AwayFromZero);
            return Math.Max(1, minutes);
        }

        private static float GetGrowthMultiplier(Region? region, TreeType treeType, ZoneDefinition zone, float x, float y, List<ZoneDefinition> zones)
        {
            float regionFactor = 1f + (region?.GrowthSpeedBonus ?? 0f);
            float treeFactor = 1f / Math.Max(0.65f, treeType.GrowthTimeMultiplier);
            float nearWater = IsNearWater(x, y, zones) ? 1.1f : 1f;
            return regionFactor * zone.GrowthMultiplier * treeFactor * nearWater;
        }

        private static float GetWaterCostMultiplier(ZoneDefinition zone, float x, float y, List<ZoneDefinition> zones)
        {
            float multiplier = zone.WaterCostMultiplier;
            if (zone.ZoneType == "Desert" || zone.ZoneType == "Dry")
                multiplier += 0.1f;
            if (zone.ZoneType != "Water" && IsNearWater(x, y, zones))
                multiplier -= 0.08f;
            return Math.Clamp(multiplier, 0.65f, 1.8f);
        }

        private static bool IsNearWater(float x, float y, List<ZoneDefinition> zones)
        {
            var waterZones = zones.Where(z => z.ZoneType == "Water");
            foreach (var zone in waterZones)
            {
                float expandedX = zone.X - 140;
                float expandedY = zone.Y - 140;
                float expandedW = zone.Width + 280;
                float expandedH = zone.Height + 280;
                if (x >= expandedX && x <= expandedX + expandedW && y >= expandedY && y <= expandedY + expandedH)
                    return true;
            }
            return false;
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

using JasylEl.Models;
using JasylEl.Data;
using Microsoft.EntityFrameworkCore;

namespace JasylEl.Repositories
{
    // ===================== ИНТЕРФЕЙСЫ =====================

    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(int id);
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetByUsernameAsync(string username);
        Task<User?> GetByEmailOrUsernameAsync(string emailOrUsername);
        Task<User> CreateAsync(User user);
        Task UpdateAsync(User user);
        Task<List<User>> GetLeaderboardAsync(int count = 20);
        Task<int> GetTreesPlantedCountAsync(int userId);
    }

    public interface IMapRepository
    {
        Task<List<MapObject>> GetUserMapObjectsAsync(int userId);
        Task<MapObject?> GetByIdAsync(int id);
        Task<MapObject> CreateAsync(MapObject obj);
        Task UpdateAsync(MapObject obj);
        Task DeleteAsync(int id);
    }

    public interface ITreeRepository
    {
        Task<List<TreeType>> GetAllAsync();
        Task<TreeType?> GetByIdAsync(int id);
        Task<List<UserUnlockedTree>> GetUserUnlockedTreesAsync(int userId);
        Task UnlockTreeForUserAsync(int userId, int treeTypeId);
        Task<bool> IsTreeUnlockedForUserAsync(int userId, int treeTypeId);
    }

    public interface IBuildingRepository
    {
        Task<List<BuildingType>> GetAllAsync();
        Task<BuildingType?> GetByIdAsync(int id);
    }

    public interface IQuizRepository
    {
        Task<List<QuizCategory>> GetCategoriesAsync();
        Task<List<QuizQuestion>> GetQuestionsByCategoryAsync(int categoryId, int difficulty = 0);
        Task<QuizQuestion?> GetQuestionByIdAsync(int id);
        Task<QuizResult> SaveResultAsync(QuizResult result);
        Task<List<QuizResult>> GetUserResultsAsync(int userId);
        Task<int> GetUserCorrectAnswersCountAsync(int userId);
    }

    public interface IAchievementRepository
    {
        Task<List<Achievement>> GetAllAsync();
        Task<List<UserAchievement>> GetUserAchievementsAsync(int userId);
        Task<UserAchievement?> GetUserAchievementAsync(int userId, int achievementId);
        Task<UserAchievement> CreateOrUpdateUserAchievementAsync(int userId, int achievementId, int progress);
        Task<List<Achievement>> GetNewlyCompletedAsync(int userId);
    }

    public interface IEventRepository
    {
        Task<List<EcoEvent>> GetActiveUserEventsAsync(int userId);
        Task<EcoEvent> CreateAsync(EcoEvent evt);
        Task ResolveAsync(int eventId);
    }

    public interface INPCRepository
    {
        Task<List<NPCType>> GetAllTypesAsync();
        Task<List<UserNPC>> GetUserNPCsAsync(int userId);
        Task<UserNPC> HireAsync(UserNPC npc);
        Task UpdatePositionAsync(int npcId, float x, float y, float tx, float ty);
    }

    public interface IRegionRepository
    {
        Task<List<Region>> GetAllAsync();
        Task<Region?> GetByIdAsync(int id);
    }

    // ===================== РЕАЛИЗАЦИИ =====================

    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _ctx;
        public UserRepository(AppDbContext ctx) => _ctx = ctx;

        public Task<User?> GetByIdAsync(int id) =>
            _ctx.Users.Include(u => u.Region).FirstOrDefaultAsync(u => u.Id == id);

        public Task<User?> GetByEmailAsync(string email) =>
            _ctx.Users.Include(u => u.Region).FirstOrDefaultAsync(u => u.Email == email);

        public Task<User?> GetByUsernameAsync(string username) =>
            _ctx.Users.Include(u => u.Region).FirstOrDefaultAsync(u => u.Username == username);

        public Task<User?> GetByEmailOrUsernameAsync(string emailOrUsername) =>
            _ctx.Users.Include(u => u.Region)
                .FirstOrDefaultAsync(u => u.Email == emailOrUsername || u.Username == emailOrUsername);

        public async Task<User> CreateAsync(User user)
        {
            _ctx.Users.Add(user);
            await _ctx.SaveChangesAsync();
            return user;
        }

        public async Task UpdateAsync(User user)
        {
            _ctx.Users.Update(user);
            await _ctx.SaveChangesAsync();
        }

        public async Task<List<User>> GetLeaderboardAsync(int count = 20)
        {
            return await _ctx.Users
                .Include(u => u.Region)
                .OrderByDescending(u => u.EcoPoints)
                .Take(count)
                .ToListAsync();
        }

        public async Task<int> GetTreesPlantedCountAsync(int userId)
        {
            return await _ctx.MapObjects
                .CountAsync(m => m.UserId == userId && m.ObjectType == "Tree" && m.IsActive);
        }
    }

    public class MapRepository : IMapRepository
    {
        private readonly AppDbContext _ctx;
        public MapRepository(AppDbContext ctx) => _ctx = ctx;

        public Task<List<MapObject>> GetUserMapObjectsAsync(int userId) =>
            _ctx.MapObjects
                .Include(m => m.TreeType)
                .Include(m => m.BuildingType)
                .Where(m => m.UserId == userId && m.IsActive)
                .ToListAsync();

        public Task<MapObject?> GetByIdAsync(int id) =>
            _ctx.MapObjects.Include(m => m.TreeType).Include(m => m.BuildingType)
                .FirstOrDefaultAsync(m => m.Id == id);

        public async Task<MapObject> CreateAsync(MapObject obj)
        {
            _ctx.MapObjects.Add(obj);
            await _ctx.SaveChangesAsync();
            return obj;
        }

        public async Task UpdateAsync(MapObject obj)
        {
            _ctx.MapObjects.Update(obj);
            await _ctx.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var obj = await _ctx.MapObjects.FindAsync(id);
            if (obj != null) { obj.IsActive = false; await _ctx.SaveChangesAsync(); }
        }
    }

    public class TreeRepository : ITreeRepository
    {
        private readonly AppDbContext _ctx;
        public TreeRepository(AppDbContext ctx) => _ctx = ctx;

        public Task<List<TreeType>> GetAllAsync() => _ctx.TreeTypes.ToListAsync();
        public Task<TreeType?> GetByIdAsync(int id) => _ctx.TreeTypes.FindAsync(id).AsTask();

        public Task<List<UserUnlockedTree>> GetUserUnlockedTreesAsync(int userId) =>
            _ctx.UserUnlockedTrees.Include(u => u.TreeType)
                .Where(u => u.UserId == userId).ToListAsync();

        public async Task UnlockTreeForUserAsync(int userId, int treeTypeId)
        {
            var exists = await _ctx.UserUnlockedTrees
                .AnyAsync(u => u.UserId == userId && u.TreeTypeId == treeTypeId);
            if (!exists)
            {
                _ctx.UserUnlockedTrees.Add(new UserUnlockedTree { UserId = userId, TreeTypeId = treeTypeId });
                await _ctx.SaveChangesAsync();
            }
        }

        public Task<bool> IsTreeUnlockedForUserAsync(int userId, int treeTypeId) =>
            _ctx.UserUnlockedTrees.AnyAsync(u => u.UserId == userId && u.TreeTypeId == treeTypeId);
    }

    public class BuildingRepository : IBuildingRepository
    {
        private readonly AppDbContext _ctx;
        public BuildingRepository(AppDbContext ctx) => _ctx = ctx;

        public Task<List<BuildingType>> GetAllAsync() => _ctx.BuildingTypes.ToListAsync();
        public Task<BuildingType?> GetByIdAsync(int id) => _ctx.BuildingTypes.FindAsync(id).AsTask();
    }

    public class QuizRepository : IQuizRepository
    {
        private readonly AppDbContext _ctx;
        public QuizRepository(AppDbContext ctx) => _ctx = ctx;

        public Task<List<QuizCategory>> GetCategoriesAsync() => _ctx.QuizCategories.ToListAsync();

        public Task<List<QuizQuestion>> GetQuestionsByCategoryAsync(int categoryId, int difficulty = 0)
        {
            var q = _ctx.QuizQuestions.Where(q => q.CategoryId == categoryId);
            if (difficulty > 0) q = q.Where(x => x.Difficulty == difficulty);
            return q.ToListAsync();
        }

        public Task<QuizQuestion?> GetQuestionByIdAsync(int id) =>
            _ctx.QuizQuestions.Include(q => q.Category).FirstOrDefaultAsync(q => q.Id == id);

        public async Task<QuizResult> SaveResultAsync(QuizResult result)
        {
            _ctx.QuizResults.Add(result);
            await _ctx.SaveChangesAsync();
            return result;
        }

        public Task<List<QuizResult>> GetUserResultsAsync(int userId) =>
            _ctx.QuizResults.Where(r => r.UserId == userId).ToListAsync();

        public Task<int> GetUserCorrectAnswersCountAsync(int userId) =>
            _ctx.QuizResults.CountAsync(r => r.UserId == userId && r.IsCorrect);
    }

    public class AchievementRepository : IAchievementRepository
    {
        private readonly AppDbContext _ctx;
        public AchievementRepository(AppDbContext ctx) => _ctx = ctx;

        public Task<List<Achievement>> GetAllAsync() => _ctx.Achievements.ToListAsync();

        public Task<List<UserAchievement>> GetUserAchievementsAsync(int userId) =>
            _ctx.UserAchievements.Include(a => a.Achievement)
                .Where(a => a.UserId == userId).ToListAsync();

        public Task<UserAchievement?> GetUserAchievementAsync(int userId, int achievementId) =>
            _ctx.UserAchievements.FirstOrDefaultAsync(a => a.UserId == userId && a.AchievementId == achievementId);

        public async Task<UserAchievement> CreateOrUpdateUserAchievementAsync(int userId, int achievementId, int progress)
        {
            var ua = await _ctx.UserAchievements
                .Include(a => a.Achievement)
                .FirstOrDefaultAsync(a => a.UserId == userId && a.AchievementId == achievementId);

            if (ua == null)
            {
                var achievement = await _ctx.Achievements.FindAsync(achievementId);
                ua = new UserAchievement
                {
                    UserId = userId,
                    AchievementId = achievementId,
                    CurrentProgress = progress
                };
                if (achievement != null && progress >= achievement.TargetValue)
                {
                    ua.IsCompleted = true;
                    ua.CompletedAt = DateTime.UtcNow;
                }
                _ctx.UserAchievements.Add(ua);
            }
            else
            {
                ua.CurrentProgress = Math.Max(ua.CurrentProgress, progress);
                if (!ua.IsCompleted && ua.Achievement != null && ua.CurrentProgress >= ua.Achievement.TargetValue)
                {
                    ua.IsCompleted = true;
                    ua.CompletedAt = DateTime.UtcNow;
                }
            }

            await _ctx.SaveChangesAsync();
            return ua;
        }

        public Task<List<Achievement>> GetNewlyCompletedAsync(int userId) =>
            _ctx.UserAchievements
                .Include(a => a.Achievement)
                .Where(a => a.UserId == userId && a.IsCompleted && a.CompletedAt > DateTime.UtcNow.AddMinutes(-1))
                .Select(a => a.Achievement!)
                .ToListAsync();
    }

    public class EventRepository : IEventRepository
    {
        private readonly AppDbContext _ctx;
        public EventRepository(AppDbContext ctx) => _ctx = ctx;

        public Task<List<EcoEvent>> GetActiveUserEventsAsync(int userId) =>
            _ctx.EcoEvents.Where(e => e.UserId == userId && !e.IsResolved).ToListAsync();

        public async Task<EcoEvent> CreateAsync(EcoEvent evt)
        {
            _ctx.EcoEvents.Add(evt);
            await _ctx.SaveChangesAsync();
            return evt;
        }

        public async Task ResolveAsync(int eventId)
        {
            var evt = await _ctx.EcoEvents.FindAsync(eventId);
            if (evt != null) { evt.IsResolved = true; evt.ResolvedAt = DateTime.UtcNow; await _ctx.SaveChangesAsync(); }
        }
    }

    public class NPCRepository : INPCRepository
    {
        private readonly AppDbContext _ctx;
        public NPCRepository(AppDbContext ctx) => _ctx = ctx;

        public Task<List<NPCType>> GetAllTypesAsync() => _ctx.NPCTypes.ToListAsync();

        public Task<List<UserNPC>> GetUserNPCsAsync(int userId) =>
            _ctx.UserNPCs.Include(n => n.NPCType)
                .Where(n => n.UserId == userId && n.IsActive).ToListAsync();

        public async Task<UserNPC> HireAsync(UserNPC npc)
        {
            _ctx.UserNPCs.Add(npc);
            await _ctx.SaveChangesAsync();
            return npc;
        }

        public async Task UpdatePositionAsync(int npcId, float x, float y, float tx, float ty)
        {
            var npc = await _ctx.UserNPCs.FindAsync(npcId);
            if (npc != null)
            {
                npc.PositionX = x; npc.PositionY = y;
                npc.TargetX = tx; npc.TargetY = ty;
                await _ctx.SaveChangesAsync();
            }
        }
    }

    public class RegionRepository : IRegionRepository
    {
        private readonly AppDbContext _ctx;
        public RegionRepository(AppDbContext ctx) => _ctx = ctx;

        public Task<List<Region>> GetAllAsync() => _ctx.Regions.ToListAsync();
        public Task<Region?> GetByIdAsync(int id) => _ctx.Regions.FindAsync(id).AsTask();
    }
}

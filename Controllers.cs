using JasylEl.DTOs;
using JasylEl.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace JasylEl.Controllers
{
    // ===================== AUTH CONTROLLER =====================

    /// <summary>Контроллер авторизации — регистрация и вход</summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        public AuthController(IAuthService authService) => _authService = authService;

        /// <summary>Регистрация нового пользователя</summary>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            try
            {
                var result = await _authService.RegisterAsync(dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Вход в систему</summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            try
            {
                var result = await _authService.LoginAsync(dto);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }
    }

    // ===================== USER CONTROLLER =====================

    /// <summary>Контроллер профиля пользователя</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IGameService _gameService;
        private readonly IQuizService _quizService;

        public UserController(IGameService gameService, IQuizService quizService)
        {
            _gameService = gameService;
            _quizService = quizService;
        }

        /// <summary>Получить профиль текущего пользователя</summary>
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            int userId = GetUserId();
            var profile = await _gameService.GetProfileAsync(userId);
            return Ok(profile);
        }

        /// <summary>Получить достижения пользователя</summary>
        [HttpGet("achievements")]
        public async Task<IActionResult> GetAchievements()
        {
            int userId = GetUserId();
            var achievements = await _quizService.GetUserAchievementsAsync(userId);
            return Ok(achievements);
        }

        /// <summary>Получить рейтинг игроков</summary>
        [HttpGet("leaderboard")]
        [AllowAnonymous]
        public async Task<IActionResult> GetLeaderboard()
        {
            var leaderboard = await _gameService.GetLeaderboardAsync();
            return Ok(leaderboard);
        }

        private int GetUserId() =>
            int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    }

    // ===================== MAP CONTROLLER =====================

    /// <summary>Контроллер карты — посадка деревьев и постройки</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MapController : ControllerBase
    {
        private readonly IGameService _gameService;
        public MapController(IGameService gameService) => _gameService = gameService;

        /// <summary>Получить все объекты на карте</summary>
        [HttpGet]
        public async Task<IActionResult> GetMap()
        {
            int userId = GetUserId();
            var map = await _gameService.GetMapAsync(userId);
            return Ok(map);
        }

        /// <summary>Проверить можно ли посадить дерево в точке</summary>
        [HttpGet("can-plant")]
        public async Task<IActionResult> CanPlant([FromQuery] float x, [FromQuery] float y, [FromQuery] int treeTypeId)
        {
            int userId = GetUserId();
            var result = await _gameService.CanPlantAtAsync(userId, x, y, treeTypeId);
            return Ok(result);
        }

        /// <summary>Посадить дерево</summary>
        [HttpPost("plant")]
        public async Task<IActionResult> PlantTree([FromBody] PlantTreeDto dto)
        {
            try
            {
                int userId = GetUserId();
                var obj = await _gameService.PlantTreeAsync(userId, dto);
                return Ok(obj);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Разместить постройку</summary>
        [HttpPost("build")]
        public async Task<IActionResult> PlaceBuilding([FromBody] PlaceBuildingDto dto)
        {
            try
            {
                int userId = GetUserId();
                var obj = await _gameService.PlaceBuildingAsync(userId, dto);
                return Ok(obj);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Улучшить постройку</summary>
        [HttpPost("upgrade")]
        public async Task<IActionResult> UpgradeBuilding([FromBody] UpgradeBuildingDto dto)
        {
            try
            {
                int userId = GetUserId();
                var obj = await _gameService.UpgradeBuildingAsync(userId, dto);
                return Ok(obj);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Получить доступные типы деревьев</summary>
        [HttpGet("trees")]
        public async Task<IActionResult> GetAvailableTrees()
        {
            int userId = GetUserId();
            var trees = await _gameService.GetAvailableTreesAsync(userId);
            return Ok(trees);
        }

        /// <summary>Получить доступные постройки</summary>
        [HttpGet("buildings")]
        public async Task<IActionResult> GetAvailableBuildings()
        {
            int userId = GetUserId();
            var buildings = await _gameService.GetAvailableBuildingsAsync(userId);
            return Ok(buildings);
        }

        /// <summary>Создать случайное событие (для тестирования)</summary>
        [HttpPost("spawn-event")]
        public async Task<IActionResult> SpawnEvent()
        {
            int userId = GetUserId();
            await _gameService.SpawnRandomEventAsync(userId);
            return Ok(new { message = "Событие создано" });
        }

        /// <summary>Решить экологическое событие</summary>
        [HttpPost("resolve-event/{eventId}")]
        public async Task<IActionResult> ResolveEvent(int eventId)
        {
            int userId = GetUserId();
            var msg = await _gameService.ResolveEventAsync(userId, eventId);
            return Ok(new { message = msg });
        }

        private int GetUserId() =>
            int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    }

    // ===================== QUIZ CONTROLLER =====================

    /// <summary>Контроллер викторин</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class QuizController : ControllerBase
    {
        private readonly IQuizService _quizService;
        public QuizController(IQuizService quizService) => _quizService = quizService;

        /// <summary>Получить категории викторин</summary>
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            int userId = GetUserId();
            var categories = await _quizService.GetCategoriesAsync(userId);
            return Ok(categories);
        }

        /// <summary>Получить вопросы по категории</summary>
        [HttpGet("questions/{categoryId}")]
        public async Task<IActionResult> GetQuestions(int categoryId, [FromQuery] int difficulty = 0)
        {
            var questions = await _quizService.GetQuestionsAsync(categoryId, difficulty);
            return Ok(questions);
        }

        /// <summary>Отправить ответ на вопрос</summary>
        [HttpPost("answer")]
        public async Task<IActionResult> SubmitAnswer([FromBody] SubmitAnswerDto dto)
        {
            try
            {
                int userId = GetUserId();
                var result = await _quizService.SubmitAnswerAsync(userId, dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private int GetUserId() =>
            int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    }

    // ===================== REGIONS CONTROLLER =====================

    /// <summary>Контроллер регионов Казахстана</summary>
    [ApiController]
    [Route("api/[controller]")]
    public class RegionsController : ControllerBase
    {
        private readonly JasylEl.Repositories.IRegionRepository _regionRepo;
        public RegionsController(JasylEl.Repositories.IRegionRepository regionRepo) =>
            _regionRepo = regionRepo;

        /// <summary>Получить все регионы</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var regions = await _regionRepo.GetAllAsync();
            return Ok(regions.Select(r => new RegionDto
            {
                Id = r.Id, Name = r.Name, Description = r.Description,
                EnergyBonus = r.EnergyBonus, WaterBonus = r.WaterBonus,
                GrowthSpeedBonus = r.GrowthSpeedBonus, CoinsBonus = r.CoinsBonus,
                ExperienceBonus = r.ExperienceBonus, DroughtRisk = r.DroughtRisk,
                StabilityBonus = r.StabilityBonus,
                PrimaryColor = r.PrimaryColor, MapTexture = r.MapTexture
            }));
        }
    }
}

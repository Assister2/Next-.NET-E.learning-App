﻿using E_learningAppBack.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Route("api/chapters")]
[ApiController]
public class MainController : ControllerBase
{
    private readonly e_learningContext _context;

    public MainController(e_learningContext context)
    {
        _context = context;
    }

    [HttpPost("register")]
    public async Task<ActionResult<User>> Register(UserRegistrationModel model)
    {
        try
        {
            if (await _context.Users.AnyAsync(u => u.Username == model.Username))
            {
                return BadRequest("Username is already taken");
            }

            var newUser = new User
            {
                Username = model.Username,
                Password = model.Password,
                Email = model.Email,
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            return Ok(newUser);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "An error occurred while processing your request.");
        }
    }


    [HttpPost("login")]
    public async Task<ActionResult<string>> Login(UserCredentials userCredentials)
    {
        try
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userCredentials.Email && u.Password == userCredentials.Password);

            if (user == null)
            {
                return BadRequest("Invalid username or password");
            }

            var token = GenerateRandomToken();

            return Ok(new
            {
                Token = token,
                User = new
                {
                    Id = user.UserId,
                    Username = user.Username,
                    Email = user.Email,
                    TotalScore = user.TotalScore
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred while processing the request : {ex.Message}");
        }
    }


    private string GenerateRandomToken()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var random = new Random();
        var token = new string(Enumerable.Repeat(chars, 32)
          .Select(s => s[random.Next(s.Length)]).ToArray());

        return token;
    }




    [HttpGet("chapterdata/{chapterId}")]
    public IActionResult GetQuizDataByChapter(int chapterId)
    {
        try
        {

            var chapterData = _context.Chapters
                .Where(chapter => chapter.ChapterId == chapterId)
                .Select(chapter => new ChapterData
                {
                    Chapter = new ChapterInfo
                    {
                        Id = chapter.ChapterId,
                        Name = chapter.ChapterName
                    },
                    Course = _context.Courses
                        .Where(course => course.ChapterId == chapter.ChapterId)
                        .Select(course => new CourseInfo
                        {
                            Id = course.CourseId,
                            Name = course.CourseName
                        })
                        .FirstOrDefault(),
                    Videos = _context.Videos
                        .Where(video => video.CourseId == _context.Courses
                            .Where(course => course.ChapterId == chapter.ChapterId)
                            .Select(course => course.CourseId)
                            .FirstOrDefault())
                        .Select(video => new VideoInfo
                        {
                            Id = video.VideoId,
                            Title = video.VideoTitle,
                            url = video.VideoUrl
                        })
                        .ToList(),
                    Quiz = _context.Quizzes
                        .Where(quiz => quiz.ChapterId == chapter.ChapterId)
                        .Select(quiz => new QuizInfo
                        {
                            Id = quiz.QuizId,
                            Title = quiz.QuizTitle
                        })
                        .FirstOrDefault(),
                    Questions = _context.Questions
                        .Where(question => question.QuizId == _context.Quizzes
                            .Where(quiz => quiz.ChapterId == chapter.ChapterId)
                            .Select(quiz => quiz.QuizId)
                            .FirstOrDefault())
                        .Select(question => new QuestionInfo
                        {
                            Id = question.QuestionId,
                            Text = question.QuestionText,
                            Options = new List<OptionInfo>
                            {
                        new OptionInfo { Answer = question.Option1 },
                        new OptionInfo { Answer = question.Option2 },
                        new OptionInfo { Answer = question.Option3 },
                        new OptionInfo { Answer = question.Option4 },
                            }
                        })
                        .ToList()
                })
                .FirstOrDefault();

            if (chapterData == null)
            {
                return NotFound("Chapter data not found");
            }

            return Ok(chapterData);

        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred while processing the request : {ex.Message}");


        }
    }

    [HttpPost("{chapterId}")]
    public async Task<IActionResult> SubmitChapterScore(int chapterId, [FromBody] AnswerModel scoreRequest)
    {
        try
        {

            var questions = await _context.Questions.Where(q => q.QuizId == scoreRequest.QuizId).ToListAsync();

            if (questions == null || questions.Count == 0)
            {
                return NotFound("No questions found for the given QuizID");
            }

            double correctAnswers = 0;
            double totalQuestions = questions.Count;


            foreach (var answer in scoreRequest.Questions)
            {
                var question = questions.FirstOrDefault(q => q.QuestionId == answer.QuestionId);

                if (question != null && question.CorrectOption == answer.Answer)
                {
                    correctAnswers++;
                    Console.WriteLine(correctAnswers);
                }

            }
            //Console.WriteLine(correctAnswers);
            //Console.WriteLine(totalQuestions);



            double scorePercentage = (correctAnswers / totalQuestions) * 100.0;
            //Console.WriteLine(scorePercentage);


            var chapter = _context.Chapters.Find(chapterId);
            if (chapter != null)
            {
                chapter.ScoreChapter = scorePercentage;
                _context.SaveChanges();
            }
            else
            {
                return NotFound("Chapter not found");
            }



            var userQuiz = new Userquiz
            {
                UserId = scoreRequest.UserId,
                QuizId = scoreRequest.QuizId,
                Score = scorePercentage,
            };

            _context.Userquizzes.Add(userQuiz);
            await _context.SaveChangesAsync();

            return Ok(new { Score = scorePercentage });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred while processing the request : {ex.Message}");


        }
    }


    [HttpPost("totalscore/{userId}")]
    public async Task<IActionResult> UpdateUserTotalScore(int userId)
    {
        try
        {

            var userChapters = await _context.Chapters
                .Where(chapter => chapter.UserID == userId)
                .ToListAsync();

            if (userChapters == null || userChapters.Count == 0)
            {
                return NotFound("No chapters found for the given user");
            }

            double totalScore = userChapters.Sum(chapter => chapter.ScoreChapter);

            var user = await _context.Users.FindAsync(userId);

            if (user == null)
            {
                return NotFound("User not found");
            }

            user.TotalScore = totalScore;

            await _context.SaveChangesAsync();

            return Ok(new { TotalScore = totalScore });

        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred while processing the request : {ex.Message}");


        }
    }




}




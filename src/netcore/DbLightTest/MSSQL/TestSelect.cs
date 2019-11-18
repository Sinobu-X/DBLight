using System;
using System.Linq;
using System.Threading.Tasks;
using DbLight;
using DbLight.Sql;
using Newtonsoft.Json;
using NUnit.Framework;

namespace DbLightTest.MSSQL
{
    public class TestSelect : TestBase
    {
        [Test]
        public void Top(){
            var db = new DbContext(GetConnection());
            var query = db.Query<User>()
                .Top(3)
                .OrderBy(x => x.UserId);

            Console.WriteLine(query.ToString());
            Console.WriteLine(JsonConvert.SerializeObject(query.ToList()));
        }

        [Test]
        public void Distinct(){
            var db = new DbContext(GetConnection());
            var query = db.Query<User>()
                .Distinct()
                .Select(x => x.SexId)
                .OrderBy(x => x.SexId);

            Console.WriteLine(query.ToString());
            Console.WriteLine(JsonConvert.SerializeObject(query.ToList(x => x.SexId)));
        }

        [Test]
        public void IgnoreColumn(){
            var db = new DbContext(GetConnection());
            var query = db.Query<User>()
                .SelectWithIgnore(x => x, x => new{
                    x.Photo,
                    x.Height
                });

            Console.WriteLine(query.ToString());
            Console.WriteLine(JsonConvert.SerializeObject(query.ToList()));
        }

        [Test]
        public void ExpressColumn(){
            var db = new DbContext(GetConnection());
            var query = db.Query<User>()
                .Select(x => x.UserId)
                .Select(x => x.Income, "{0} - 100.00::money", x => x.Income);

            Console.WriteLine(query.ToString());
            Console.WriteLine(JsonConvert.SerializeObject(query.ToList()));
        }

        [Test]
        public void ChildQueryAtColumn(){

            var db = new DbContext(GetConnection());
            var query = db.Query<(User User, int MaxRoleId)>()
                .Select(x => new{
                    x.User.UserId,
                    x.User.Income
                })
                .Select(x => x.MaxRoleId, db.ChildQuery<Role>().Max(x => x.RoleId));

            Console.WriteLine(query.ToString());
            Console.WriteLine(JsonConvert.SerializeObject(query.ToList(x => new{
                UserId = x.User.UserId,
                Income = x.User.Income,
                MaxRoleId = x.MaxRoleId
            })));
        }

        [Test]
        public async Task Max(){
            var db = new DbContext(GetConnection());
            var maxUserId = await db.Query<User>()
                .Max(x => x.UserId)
                .ToFirstAsync(x => x.UserId);

            Console.WriteLine($"Max Id = {maxUserId}");
        }

        [Test]
        public void MaxAndCount(){
            var db = new DbContext(GetConnection());
            var query = db.Query<(User User, long Count)>()
                .Select(x => x.User.SexId)
                .Max(x => x.User.UserId)
                .Count(x => x.Count)
                .GroupBy(x => x.User.SexId)
                .OrderBy(x => x.User.SexId);

            Console.WriteLine(query.ToString());
            Console.WriteLine(JsonConvert.SerializeObject(query.ToList(x => new{
                SexId = x.User.SexId,
                UserId = x.User.UserId,
                Count = x.Count
            })));
        }


        [Test]
        public void LeftJoin(){
            var db = new DbContext(GetConnection());
            var query = db.Query<(User User, Sex Sex)>()
                .Select(x => new{
                    x.User,
                    x.Sex
                })
                .LeftJoin(x => x.Sex, x => x.Sex.SexId == x.User.SexId)
                .Where(x => x.User.UserId > 0 && x.Sex.SexId != 2);

            Console.WriteLine(query.ToString());
            Console.WriteLine(
                JsonConvert.SerializeObject(query.ToList()));
        }

        [Test]
        public void InnerJoin(){
            var db = new DbContext(GetConnection());
            var query = db.Query<(User User, Sex Sex)>()
                .Select(x => new{
                    x.User,
                    x.Sex
                })
                .InnerJoin(x => x.Sex, x => x.Sex.SexId == x.User.SexId)
                .Where(x => x.User.UserId > 0 && x.Sex.SexId != 2);

            Console.WriteLine(query.ToString());
            Console.WriteLine(
                JsonConvert.SerializeObject(query.ToList()));
        }

        [Test]
        public void UnionAll(){
            var db = new DbContext(GetConnection());
            var query = db.Query<User>()
                .Select(x => new{
                    x.UserId,
                    x.UserName
                })
                .Where(x => x.UserId == 4)
                .UnionAll(db.Query<User>()
                    .Select(x => new{
                        x.UserId,
                        x.UserName
                    })
                    .Where(x => x.UserId == 3)
                );

            Console.WriteLine(query.ToString());
            Console.WriteLine(
                JsonConvert.SerializeObject(query.ToList()));
        }


        [Test]
        public void WhereLike(){
            var db = new DbContext(GetConnection());
            var query = db.Query<User>()
                .WhereBegin()
                .Like(x => x.UserName, SqlLikeType.Equal, "name 12")
                .WhereEnded();

            Console.WriteLine(query.ToString());
            Console.WriteLine(
                JsonConvert.SerializeObject(query.ToList()));
        }


        [Test]
        public void WhereInArray(){
            var db = new DbContext(GetConnection());

            var userIds = new[]{1, 2, 3};
            var userNames = new[]{"a", "b", "c"};

            {
                var query = db.Query<User>()
                    .Where(x => userIds.Contains(x.UserId) || userNames.Contains(x.UserName));

                Console.WriteLine(query.ToString());
                Console.WriteLine(
                    JsonConvert.SerializeObject(query.ToList()));
            }

            {
                var query = db.Query<User>()
                    .WhereBegin(SqlWhereJoinType.Or)
                    .In(x => x.UserId, userIds)
                    .In(x => x.UserName, userNames)
                    .WhereEnded();

                Console.WriteLine(query.ToString());
                Console.WriteLine(
                    JsonConvert.SerializeObject(query.ToList()));
            }
        }

        [Test]
        public void WhereInQuery(){
            var db = new DbContext(GetConnection());

            //SELECT * FROM "public"."user" AS "a"
            //WHERE "a"."user_id" > 3
            //AND "a"."user_id" IN(
            //    SELECT "a"."user_id" AS "user_id"
            //    FROM "public"."role_user" AS "a"
            //    WHERE "a"."role_id" = 4)

            {
                var query = db.Query<User>()
                    .Where(x => x.UserId > 3 &&
                                db.ChildQuery<RoleUser>()
                                    .Select(y => y.UserId)
                                    .Where(y => y.RoleId == 4).Contains(x.UserId));

                Console.WriteLine(query.ToString());
                Console.WriteLine(
                    JsonConvert.SerializeObject(query.ToList()));
            }

            {
                var query = db.Query<User>()
                    .WhereBegin()
                    .Compare(x => x.UserId, SqlCompareType.Greater, 3)
                    .Add(x => db.ChildQuery<RoleUser>()
                        .Select(y => y.UserId)
                        .WhereBegin(SqlWhereJoinType.And)
                        .Compare(y => y.RoleId, SqlCompareType.Equal, 4)
                        .WhereEnded()
                        .Contains(x.UserId))
                    .WhereEnded();

                Console.WriteLine(query.ToString());
                Console.WriteLine(
                    JsonConvert.SerializeObject(query.ToList()));
            }

        }

        [Test]
        public void WhereInExpress(){
            var db = new DbContext(GetConnection());

            {
                var query = db.Query<User>()
                    .Where(x => db.Exp("SELECT UserId From [RoleUser] WHERE [RoleId] = 1").Contains(x.UserId));

                Console.WriteLine(query.ToString());
                Console.WriteLine(
                    JsonConvert.SerializeObject(query.ToList()));
            }
        }
    }
}
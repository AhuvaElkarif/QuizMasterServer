using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

public class Result
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

    public string UserId { get; set; }
    public string ExamId { get; set; }
    public int Score { get; set; }
}

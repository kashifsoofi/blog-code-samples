package store

import (
	"context"
	"time"

	"github.com/google/uuid"
	"github.com/kashifsoofi/blog-code-samples/movies-api-with-go-chi-and-mongodb/config"
	"go.mongodb.org/mongo-driver/bson"
	"go.mongodb.org/mongo-driver/mongo"
	"go.mongodb.org/mongo-driver/mongo/options"
)

type MongoMoviesStore struct {
	database   config.Database
	client     *mongo.Client
	collection *mongo.Collection
}

func NewMongoMoviesStore(config config.Database) *MongoMoviesStore {
	return &MongoMoviesStore{
		database: config,
	}
}

func (s *MongoMoviesStore) connect(ctx context.Context) error {
	serverAPI := options.ServerAPI(options.ServerAPIVersion1)

	client, err := mongo.Connect(
		ctx,
		options.Client().ApplyURI(s.database.DatabaseURL).SetServerAPIOptions(serverAPI),
	)
	if err != nil {
		return err
	}

	s.client = client
	s.collection = s.client.Database(s.database.DatabaseName).Collection(s.database.MoviesCollectionName)
	return nil
}

func (s *MongoMoviesStore) close(ctx context.Context) error {
	return s.client.Disconnect(ctx)
}

func (s *MongoMoviesStore) Create(ctx context.Context, createMovieParams CreateMovieParams) error {
	err := s.connect(ctx)
	if err != nil {
		return err
	}
	defer s.close(ctx)

	movie := Movie{
		ID:          createMovieParams.ID,
		Title:       createMovieParams.Title,
		Director:    createMovieParams.Director,
		ReleaseDate: createMovieParams.ReleaseDate,
		TicketPrice: createMovieParams.TicketPrice,
		CreatedAt:   time.Now().UTC(),
		UpdatedAt:   time.Now().UTC(),
	}

	if _, err := s.collection.InsertOne(ctx, movie); err != nil {
		if mongo.IsDuplicateKeyError(err) {
			return &DuplicateKeyError{ID: createMovieParams.ID}
		}
		return err
	}

	return nil
}

func (s *MongoMoviesStore) GetAll(ctx context.Context) ([]Movie, error) {
	err := s.connect(ctx)
	if err != nil {
		return nil, err
	}
	defer s.close(ctx)

	cur, err := s.collection.Find(ctx, bson.D{})
	if err != nil {
		return nil, err
	}
	defer cur.Close(ctx)

	var movies []Movie
	if err := cur.All(ctx, &movies); err != nil {
		return nil, err
	}

	return movies, nil
}

func (s *MongoMoviesStore) GetByID(ctx context.Context, id uuid.UUID) (Movie, error) {
	err := s.connect(ctx)
	if err != nil {
		return Movie{}, err
	}
	defer s.close(ctx)

	var movie Movie
	if err := s.collection.FindOne(ctx, bson.M{"_id": id}).Decode(&movie); err != nil {
		if err == mongo.ErrNoDocuments {
			return Movie{}, &RecordNotFoundError{}
		}
		return Movie{}, err
	}

	return movie, nil
}

func (s *MongoMoviesStore) Update(ctx context.Context, id uuid.UUID, updateMovieParams UpdateMovieParams) error {
	err := s.connect(ctx)
	if err != nil {
		return err
	}
	defer s.close(ctx)

	update := bson.M{
		"$set": bson.M{
			"Title":       updateMovieParams.Title,
			"Director":    updateMovieParams.Director,
			"ReleaseDate": updateMovieParams.ReleaseDate,
			"TicketPrice": updateMovieParams.TicketPrice,
			"UpdatedAt":   time.Now().UTC(),
		},
	}
	if _, err := s.collection.UpdateOne(ctx, bson.M{"_id": id}, update); err != nil {
		return err
	}

	return nil
}

func (s *MongoMoviesStore) Delete(ctx context.Context, id uuid.UUID) error {
	err := s.connect(ctx)
	if err != nil {
		return err
	}
	defer s.close(ctx)

	if _, err := s.collection.DeleteOne(ctx, bson.M{"_id": id}); err != nil {
		return err
	}

	return nil
}

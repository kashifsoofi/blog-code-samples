package store

import (
	"fmt"

	"github.com/google/uuid"
)

type DuplicateIdError struct {
	Id uuid.UUID
}

func (e *DuplicateIdError) Error() string {
	return fmt.Sprintf("duplicate movie id: %v", e.Id)
}

type RecordNotFoundError struct{}

func (e *RecordNotFoundError) Error() string {
	return "record not found"
}

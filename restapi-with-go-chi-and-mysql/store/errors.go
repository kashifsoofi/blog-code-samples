package store

import (
	"fmt"

	"github.com/google/uuid"
)

type DuplicateIDError struct {
	ID uuid.UUID
}

func (e *DuplicateIDError) Error() string {
	return fmt.Sprintf("duplicate movie id: %v", e.ID)
}

type RecordNotFoundError struct{}

func (e *RecordNotFoundError) Error() string {
	return "record not found"
}

CC ?= gcc
PKGCONFIG = $(shell which pkg-config)
CFLAGS = $(shell $(PKGCONFIG) --cflags gtk4)
LIBS = $(shell $(PKGCONFIG) --libs gtk4)

SRC = main.c

OBJS = $(.c=.o) $(SRC:.c=.o)

all: counterapp

%.o: %.c
	$(CC) -c -o $(@F) $(CFLAGS) $<

counterapp: $(OBJS)
	$(CC) -o $(@F) $(OBJS) $(LIBS)

clean:
	rm -f $(OBJS)
	rm -f counterapp

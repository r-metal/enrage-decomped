#!/bin/sh
# Repo invariants, over everything that is or could be tracked (tracked files plus
# untracked ones that aren't ignored):
#   1. no binaries: game files, mod DLLs, Script Hook files, archives
#   2. no line of .private-patterns (local, untracked) in any file. A match is reported as
#      `file: pattern #N`, never by its text. Public clones have no such file: skipped.
#   3. every commit reachable from HEAD has author and committer dates at +0000
# Prints `invariants: OK` last when all hold, exits 1 otherwise.
cd "$(dirname "$0")/.." || exit 2
tmp=$(mktemp -d) || exit 2
trap 'rm -rf "$tmp"' EXIT
git ls-files -co --exclude-standard -z > "$tmp/files" || { echo "invariants: not a git checkout"; exit 2; }
names() { tr '\0' '\n' < "$tmp/files"; }
bad=0

bins=$(names | grep -iE '\.(dll|exe|pdb|asi|rar|zip|7z)$')
if [ -n "$bins" ]; then
	echo "$bins" | sed 's/$/: binary file/'
	bad=1
fi

if [ -f .private-patterns ]; then
	tr -d '\r' < .private-patterns > "$tmp/patterns"
	n=0
	while read -r p || [ -n "$p" ]; do
		n=$((n + 1))
		[ -n "$p" ] || continue
		# a path that holds the pattern would print it: name the case, not the path
		if names | grep -qiF -- "$p"; then
			echo "(a file path): pattern #$n"
			bad=1
		fi
		hits=$(xargs -0 grep -ilsF -- "$p" < "$tmp/files" | grep -viF -- "$p")
		if [ -n "$hits" ]; then
			echo "$hits" | sed "s/\$/: pattern #$n/"
			bad=1
		fi
	done < "$tmp/patterns"
else
	echo "invariants: no .private-patterns - identity scan skipped"
fi

if git rev-parse -q --verify HEAD > /dev/null; then
	dates=$(git log --no-show-signature --format='%h %ad %cd' --date=raw HEAD |
		awk '$3 != "+0000" || $5 != "+0000" { print "commit " $1 ": author " $3 ", committer " $5 " (must be +0000)" }')
	if [ -n "$dates" ]; then
		echo "$dates"
		bad=1
	fi
fi

if [ $bad -ne 0 ]; then
	echo "invariants: FAILED"
	exit 1
fi
echo "invariants: OK"

#!/bin/bash

# reset to upstream to get all commits
git remote add upstream https://github.com/MirrorNetworking/Mirror.git
git fetch --all
git fetch --all --tags --force
# reset files back to origin
git checkout master
git reset --hard upstream/master
# remove mirror's workflow
rm -r .github

# get workflow files, these include release config and meta files for Mirror folder
git checkout origin/workflow -- .

git commit -m "CI: adding package workflow"

# move files and create package.json
mv Assets/Mirror/Tests Assets/Tests
mv Assets/Mirror/Examples Assets/Mirror/Samples~

git add .
git commit -m "CI: moving tests and samples"

sample_array=''

# Find all directories in the samples directory
for dir in $(find Assets/Mirror/Samples~ -type d -maxdepth 1 -mindepth 1)
do
    # Remove the leading './' from the directory name
    trimmed_path="${dir#Assets/Mirror/}"
    dir_name=$trimmed_path

    # Add the directory to the JSON array
    sample_array=$sample_array'{"displayName": "'$dir_name'", "description": "", "path": "'$dir_name'"},'
done

# Remove the trailing comma from the JSON array
sample_array="${sample_array%,}"

# Close the JSON array
latest_tag=$(git describe --tags --abbrev=0 upstream/master)

echo '{
    "name": "com.mirrornetworking.mirror",
    "displayName": "mirror",
    "version": "'$latest_tag'",
    "unity": "2020.3",
    "description": "Mirror is a high level Networking API for Unity, supporting different low level Transports.",
    "author": "Mirror",
    "repository": {
        "type": "git",
        "url": "https://github.com/James-Frowen/Mirror.git"
    },
    "dependencies": {},
    "samples": [
        '$sample_array'
    ]
}' > Assets/Mirror/package.json


git add .
git commit -m "CI: adding package.json"

git push --delete origin $latest_tag
git push --delete $latest_tag
git tag $latest_tag

# push all tags, this needs to be done here because it causes the release step to fail
git push -f 
git push -f --tags

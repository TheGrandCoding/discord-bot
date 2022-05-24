echo Deploying to $1


cd DiscordBot/bin/Release/net6.0/linux-arm
git init
echo "*.*" >> .gitignore
echo "!.gitignore" >> .gitignore
echo "!*/*" >> .gitignore
echo "x64" >> .gitignore
echo "x86" >> .gitignore
git add .
git config --global user.email "buildbot@github.com"
git config --global user.name "Build Bot"
git commit -a -m "Automatic forcepush of build"
git remote add origin https://$2@github.com:CheAle14/bot-binary.git
git push origin HEAD:$1 --force
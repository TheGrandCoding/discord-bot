echo Deploying to $1

openssl aes-256-cbc -K $encrypted_871d352bed27_key -iv $encrypted_871d352bed27_iv -in .travis/deploy_key.pem.enc -out .travis/deploy_key.pem -d
eval "$(ssh-agent -s)" #start the ssh agent
chmod 600 .travis/deploy_key.pem # this key should have push access
ssh-add .travis/deploy_key.pem
cd DiscordBot/bin/Release/netcoreapp3.1/linux-arm
git init
echo "*.*" >> .gitignore
echo "!.gitignore" >> .gitignore
echo "!*/*" >> .gitignore
echo "x64" >> .gitignore
echo "x86" >> .gitignore
git add .
git commit -a -m "Automatic forcepush of build"
git remote add origin git@github.com:CheAle14/bot-binary.git
git push origin HEAD:$1 --force
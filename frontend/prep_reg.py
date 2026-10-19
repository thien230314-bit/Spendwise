with open(r'c:\Users\Hi\Downloads\notebook-auth.html', 'r', encoding='utf-8') as f:
    c = f.read()

# Hide login leaf and show register
c = c.replace('class="leaf" id="leaf"', 'class="leaf" id="leaf" style="display:none"')
c = c.replace('inert', '')
with open(r'c:\Users\Hi\Downloads\static-reg.html', 'w', encoding='utf-8') as f:
    f.write(c)

print('Done')
